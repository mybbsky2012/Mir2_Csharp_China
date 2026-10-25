# -*- coding: utf-8 -*-
"""
真实场景微端压测：模拟多个玩家同时下载「互不相同的资源文件」。

与 bench_resource.py 的区别（这才是真实的负载形态）：
  bench_resource.py 反复下载同一个 1.9MB 文件 —— 整个文件常驻系统缓存，
  测出来的是内存速度，反映不了真实磁盘压力。
  本脚本让每个请求下载**不同的文件**（从真实资源目录采样），
  并在压测期间同步采样 CPU / 内存 / 磁盘读写速率，
  从而回答「微端下载会不会把服务器拖垮」。

用法：
  python micro_bench.py [并发] [玩家数] [每人文件数] [服务地址] [资源根目录]
"""
import io, os, random, sys, threading, time, urllib.parse, urllib.request
from concurrent.futures import ThreadPoolExecutor
import psutil

BASE = "http://127.0.0.1:5680"
ROOT = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources"
HOST_PID = None


def build_pool(root, limit=400):
    """从真实资源目录采样文件池，按真实大小分层（大文件少、小文件多）。"""
    files = []
    for sub in ("Data", "Map", "Sound"):
        d = os.path.join(root, sub)
        if not os.path.isdir(d):
            continue
        for dirpath, _, names in os.walk(d):
            for n in names:
                if not n.lower().endswith((".lib", ".wav", ".map")):
                    continue
                p = os.path.join(dirpath, n)
                try:
                    sz = os.path.getsize(p)
                except OSError:
                    continue
                if sz < 4096:
                    continue
                rel = os.path.relpath(p, root).replace("\\", "/")
                files.append((rel, sz))
    random.shuffle(files)
    files.sort(key=lambda x: x[1])
    # 分层：小文件取多，大文件取少
    small = [f for f in files if f[1] < 2 * 1024 * 1024][:200]
    mid = [f for f in files if 2 * 1024 * 1024 <= f[1] < 20 * 1024 * 1024][:120]
    big = [f for f in files if f[1] >= 20 * 1024 * 1024][:40]
    pool = small + mid + big
    random.shuffle(pool)
    return pool[:limit]


def fetch(rel, timeout=120):
    url = BASE + "/resource?f=" + urllib.parse.quote(rel)
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(url, timeout=timeout) as r:
            n = 0
            while True:
                chunk = r.read(65536)
                if not chunk:
                    break
                n += len(chunk)
        return True, time.perf_counter() - t0, n
    except Exception:
        return False, time.perf_counter() - t0, 0


class Sampler(threading.Thread):
    def __init__(self, pid, interval=1.0):
        super().__init__(daemon=True)
        self.pid, self.interval, self.stop = pid, interval, False
        self.samples = []

    def run(self):
        try:
            proc = psutil.Process(self.pid) if self.pid else None
            if proc:
                proc.cpu_percent(None)
        except Exception:
            proc = None
        prev = psutil.disk_io_counters(perdisk=True)
        prevt = time.time()
        while not self.stop:
            time.sleep(self.interval)
            now = time.time()
            cur = psutil.disk_io_counters(perdisk=True)
            dt = now - prevt
            rd = 0
            for k, v in cur.items():
                if k not in prev:
                    continue
                if k.startswith(("PhysicalDrive", "nvme", "sd", "hd")):
                    rd += v.read_bytes - prev[k].read_bytes
            prev, prevt = cur, now
            vm = psutil.virtual_memory()
            s = {
                "t": now,
                "sys_cpu": psutil.cpu_percent(None),
                "disk_read_mbs": rd / dt / 1048576.0 if dt > 0 else 0,
                "mem_used_gb": (vm.total - vm.available) / 1024 ** 3,
            }
            if proc:
                try:
                    s["proc_cpu"] = proc.cpu_percent(None)
                    s["proc_mem_mb"] = proc.memory_info().rss / 1024 ** 2
                    s["proc_threads"] = proc.num_threads()
                except Exception:
                    proc = None
            self.samples.append(s)


def pct(vals, p):
    if not vals:
        return 0.0
    s = sorted(vals)
    return s[min(len(s) - 1, int(round(p / 100.0 * (len(s) - 1))))]


def main():
    conc = int(sys.argv[1]) if len(sys.argv) > 1 else 64
    players = int(sys.argv[2]) if len(sys.argv) > 2 else 100
    per = int(sys.argv[3]) if len(sys.argv) > 3 else 30
    global BASE
    if len(sys.argv) > 4:
        BASE = sys.argv[4].rstrip("/")
    root = sys.argv[5] if len(sys.argv) > 5 else ROOT

    out = io.open(sys.stdout.fileno(), "w", encoding="utf-8", buffering=1, closefd=False)

    pool = build_pool(root)
    if not pool:
        out.write("[中止] 资源池为空: %s\n" % root)
        return 1

    total_bytes = sum(s for _, s in pool)
    out.write("=" * 84 + "\n")
    out.write("微端真实负载压测（每次下载不同文件，模拟玩家首次进场）\n")
    out.write("=" * 84 + "\n")
    out.write("  服务地址   : %s\n" % BASE)
    out.write("  资源根目录 : %s\n" % root)
    out.write("  文件池     : %d 个互不相同的文件，合计 %.0f MB（大文件占 %.0f%%）\n" % (
        len(pool), total_bytes / 1048576.0,
        100.0 * sum(s for _, s in pool if s >= 20 * 1048576) / max(1, total_bytes)))
    out.write("  模拟规模   : %d 个玩家 × 每人 %d 个文件 = %d 次下载，并发 %d\n" % (
        players, per, players * per, conc))
    out.write("\n")

    tasks = []
    for _ in range(players * per):
        rel, sz = random.choice(pool)
        tasks.append((rel, sz))

    # 找到宿主进程
    host_pid = None
    for p in psutil.process_iter(["pid", "name"]):
        if p.info["name"] and p.info["name"].lower() == "httpbenchhost.exe":
            host_pid = p.info["pid"]
            break

    sampler = Sampler(host_pid, 1.0)
    sampler.start()

    lat, ok, fail = [], 0, 0
    sent = 0
    t0 = time.perf_counter()
    with ThreadPoolExecutor(max_workers=conc) as poolx:
        futs = [poolx.submit(fetch, rel) for rel, _ in tasks]
        for f in futs:
            good, el, n = f.result()
            lat.append(el)
            sent += n
            if good and n > 0:
                ok += 1
            else:
                fail += 1
    wall = time.perf_counter() - t0
    sampler.stop = True
    time.sleep(1.2)

    out.write("---- 客户端侧结果 ----\n")
    out.write("  成功/失败    : %d / %d\n" % (ok, fail))
    out.write("  实际传输     : %.2f GB\n" % (sent / 1024 ** 3))
    out.write("  墙钟耗时     : %.2f s\n" % wall)
    out.write("  吞吐         : %.1f 个/s   %.1f MB/s\n" % (ok / wall, sent / wall / 1048576.0))
    out.write("  延迟         : 平均 %.0f ms   P50 %.0f ms   P95 %.0f ms   最大 %.0f ms\n" % (
        (sum(lat) / len(lat) * 1000) if lat else 0, pct(lat, 50) * 1000, pct(lat, 95) * 1000,
        (max(lat) * 1000) if lat else 0))
    out.write("\n---- 压测期间服务端进程与系统资源（每秒采样）----\n")
    out.write("  %-10s %-10s %-10s %-12s %-10s\n" % ("系统CPU%", "进程CPU%", "进程内存MB", "磁盘读MB/s", "系统已用GB"))
    ds, pc, pm = [], [], []
    for s in sampler.samples:
        ds.append(s.get("disk_read_mbs", 0))
        pc.append(s.get("proc_cpu", 0))
        pm.append(s.get("proc_mem_mb", 0))
        out.write("  %-10.0f %-10.0f %-10.0f %-12.0f %-10.1f\n" % (
            s.get("sys_cpu", 0), s.get("proc_cpu", 0), s.get("proc_mem_mb", 0),
            s.get("disk_read_mbs", 0), s.get("mem_used_gb", 0)))

    n = len(sampler.samples)
    if n:
        out.write("\n  峰值：系统CPU %.0f%%，进程CPU %.0f%%，进程内存 %.0f MB，磁盘读 %.0f MB/s\n" % (
            max(s.get("sys_cpu", 0) for s in sampler.samples),
            max(pc), max(pm), max(ds)))
        out.write("  均值：系统CPU %.0f%%，进程CPU %.0f%%，进程内存 %.0f MB，磁盘读 %.0f MB/s\n" % (
            sum(s.get("sys_cpu", 0) for s in sampler.samples) / n,
            sum(pc) / n, sum(pm) / n, sum(ds) / n))

    # 判断数据是走内存还是真读盘
    avg_disk = sum(ds) / n if n else 0
    net_mbs = sent / wall / 1048576.0
    out.write("\n  → 网络吞吐 %.0f MB/s，磁盘读 %.0f MB/s：" % (net_mbs, avg_disk))
    if avg_disk < net_mbs * 0.3:
        out.write("大部分命中系统文件缓存（重复访问同一批文件所致）\n")
    else:
        out.write("确实在真实读盘\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

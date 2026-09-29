# -*- coding: utf-8 -*-
"""
微端资源服务并发压测。

目的：验证服务端 HTTP 资源服务在「多人同时下载」时的真实吞吐能力。

测法：固定总请求数，逐档提高并发数，对比墙钟耗时。
      如果服务端能并行处理，耗时应随并发提高而显著下降；
      如果耗时几乎不变（甚至变长），说明请求在服务端被串行排队。

用法：
    python bench_resource.py [并发档位,逗号分隔] [每档请求数] [目标文件]
默认：
    python bench_resource.py 1,4,16,64,128 200 Data/DNItems.Lib
"""

import io
import os
import sys
import time
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor

# 可用环境变量指向其它端口，便于对比改造前后的实现
BASE = os.environ.get("MIR_BENCH_BASE", "http://127.0.0.1:5679").rstrip("/")


def fetch(target, timeout=60):
    """单次下载。每次新建连接，模拟客户端当前「每次 new HttpClient」的行为。"""
    url = BASE + "/resource?f=" + urllib.parse.quote(target)
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(url, timeout=timeout) as resp:
            data = resp.read()
            code = resp.status
        return True, time.perf_counter() - t0, len(data), code
    except Exception as e:
        return False, time.perf_counter() - t0, 0, str(e)


def percentile(values, p):
    if not values:
        return 0.0
    s = sorted(values)
    k = min(len(s) - 1, int(round((p / 100.0) * (len(s) - 1))))
    return s[k]


def run_stage(target, concurrency, total, timeout=60):
    lat = []
    ok = 0
    fail = 0
    err_sample = None

    t0 = time.perf_counter()
    with ThreadPoolExecutor(max_workers=concurrency) as pool:
        futures = [pool.submit(fetch, target, timeout) for _ in range(total)]
        for f in futures:
            good, elapsed, size, info = f.result()
            lat.append(elapsed)
            if good:
                ok += 1
            else:
                fail += 1
                if err_sample is None:
                    err_sample = info
    wall = time.perf_counter() - t0

    return {
        "concurrency": concurrency,
        "total": total,
        "ok": ok,
        "fail": fail,
        "wall": wall,
        "qps": total / wall if wall > 0 else 0,
        "avg": sum(lat) / len(lat) if lat else 0,
        "p50": percentile(lat, 50),
        "p95": percentile(lat, 95),
        "max": max(lat) if lat else 0,
        "err": err_sample,
    }


def get_resinfo():
    try:
        with urllib.request.urlopen(BASE + "/resinfo", timeout=10) as r:
            return r.read().decode("utf-8", "replace")
    except Exception as e:
        return "resinfo 不可用: %s" % e


def get_file_size(target):
    ok, _, size, code = fetch(target, timeout=60)
    return size if ok else 0


def main():
    stages = [1, 4, 16, 64, 128]
    total = 200
    target = "Data/DNItems.Lib"

    if len(sys.argv) > 1:
        stages = [int(x) for x in sys.argv[1].split(",") if x.strip()]
    if len(sys.argv) > 2:
        total = int(sys.argv[2])
    if len(sys.argv) > 3:
        target = sys.argv[3]

    out = io.open(sys.stdout.fileno(), "w", encoding="utf-8", buffering=1, closefd=False)

    out.write("=" * 78 + "\n")
    out.write("微端资源服务并发压测\n")
    out.write("=" * 78 + "\n")
    out.write("  服务端     : %s\n" % BASE)
    out.write("  状态       : %s\n" % get_resinfo())
    out.write("  目标文件   : %s\n" % target)

    size = get_file_size(target)
    out.write("  文件大小   : %.2f MB (%d 字节)\n" % (size / 1048576.0, size))
    if size == 0:
        out.write("\n[中止] 目标文件取不到，确认服务端已开启资源服务。\n")
        return 1

    out.write("  总请求数   : 每档 %d 次\n" % total)
    out.write("  说明       : 每次请求新建 TCP 连接（模拟客户端当前行为）\n")
    out.write("\n")

    hdr = "%-8s %-10s %-10s %-10s %-10s %-10s %-10s %-10s\n" % (
        "并发", "墙钟(s)", "吞吐(个/s)", "吞吐(MB/s)", "平均(ms)", "P50(ms)", "P95(ms)", "失败")
    out.write(hdr)
    out.write("-" * 78 + "\n")

    results = []
    for c in stages:
        r = run_stage(target, c, total)
        results.append(r)
        mbps = (r["ok"] * size / 1048576.0) / r["wall"] if r["wall"] > 0 else 0
        out.write("%-8d %-10.2f %-10.1f %-10.1f %-10.0f %-10.0f %-10.0f %-10d\n" % (
            c, r["wall"], r["qps"], mbps,
            r["avg"] * 1000, r["p50"] * 1000, r["p95"] * 1000, r["fail"]))
        if r["err"]:
            out.write("         └ 失败样例: %s\n" % r["err"])

    out.write("-" * 78 + "\n")

    if len(results) >= 2:
        base = results[0]
        best = min(results, key=lambda r: r["wall"])
        out.write("\n[结论]\n")
        out.write("  并发 %d  ->  墙钟 %.2fs，平均 %.0f ms，P95 %.0f ms\n" % (
            base["concurrency"], base["wall"], base["avg"] * 1000, base["p95"] * 1000))
        out.write("  最快档  ->  并发 %d，墙钟 %.2fs，平均 %.0f ms，P95 %.0f ms\n" % (
            best["concurrency"], best["wall"], best["avg"] * 1000, best["p95"] * 1000))

        # 判断并行质量：关键不是「提速多少倍」（单请求本来就快时提速空间有限，
        # 磁盘带宽早就跑满了），而是「高并发下延迟有没有被排队拖垮」。
        load = max(results, key=lambda r: r["concurrency"])
        out.write("  最高并发 %d：平均 %.0f ms，P95 %.0f ms\n" % (
            load["concurrency"], load["avg"] * 1000, load["p95"] * 1000))

        worst_p95 = max(r["p95"] for r in results)
        light_p50 = min(r["p50"] for r in results)
        if worst_p95 > max(0.3, light_p50 * 8):
            out.write("  → 高并发下延迟成倍恶化：请求在服务端被**串行排队**\n")
        else:
            out.write("  → 延迟在各并发档位都保持低位：请求被**并行处理**，未出现串行排队\n")

        # 吞吐是否已经饱和（继续加并发不再变快）
        top = [r for r in results if r["concurrency"] >= load["concurrency"] / 2]
        if len(top) >= 2:
            qs = [r["qps"] for r in top]
            if min(qs) > 0 and max(qs) / min(qs) < 1.3:
                out.write("  → 吞吐已饱和在 %.0f 个/s（约 %.0f MB/s），这是磁盘/网卡的实际上限，\n"
                          "     继续加并发不会更快，只会排队\n" % (load["qps"], (load["ok"] * size / 1048576.0) / load["wall"]))

        # 推算 100 人同时下载的场景
        per_req_best = best["wall"] / best["total"]
        out.write("\n[百人场景推算（基于最快档的单请求成本）]\n")
        out.write("  单请求平均成本 : %.0f ms\n" % (per_req_best * 1000))
        for players, files in ((100, 20), (100, 50), (100, 200)):
            n = players * files
            est = n * per_req_best
            out.write("  %d 人 × 每人 %d 个文件 = %d 请求 → 约 %.0f 秒(%.1f 分钟) 才能发完\n" % (
                players, files, n, est, est / 60.0))
        out.write("\n  注意：以上是磁盘与内网环回的速度上限。真实部署的上限通常是\n")
        out.write("        服务器上行带宽（家庭宽带上行可能只有几 MB/s），不是这个服务的处理能力。\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

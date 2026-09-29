# -*- coding: utf-8 -*-
"""
验证「慢速下载者是否阻塞整个资源服务」。

原理：服务端 WriteBinary() 是**同步写 socket**，而 HttpService.Listen() 是
      单线程循环 —— 只有当前请求彻底发完，才会去取下一个。
      所以只要有一个客户端读得慢（外网小水管），它的写操作就会一直占着
      那唯一的处理线程，其他所有人的请求全部排队。

实验：先测一组「干净基线」，再同时挂上 N 个慢速下载者，重测同一组请求，
      对比延迟差异。若延迟暴涨，即证明是全局阻塞（而不是单纯排队）。

用法：
    python bench_slowclient.py [慢速客户端数] [慢速KB/s] [基线请求数]
默认：
    python bench_slowclient.py 1 200 30
"""

import io
import os
import socket
import sys
import threading
import time
import urllib.parse
import urllib.request

# 可用环境变量指向其它端口，便于对比改造前后的实现
_base = os.environ.get("MIR_BENCH_BASE", "http://127.0.0.1:5679")
_parsed = urllib.parse.urlparse(_base)
HOST = _parsed.hostname or "127.0.0.1"
PORT = _parsed.port or 5679
BIG_FILE = "Map/0.map"          # 7.35 MB
PROBE_FILE = "Data/DNItems.Lib"  # 1.88 MB


def slow_reader(stop_event, path, kbps, stats, idx):
    """模拟外网慢速客户端：极慢地读取响应体，长时间占住服务端写操作。"""
    chunk = 8192
    per_chunk_sleep = chunk / 1024.0 / kbps
    try:
        s = socket.create_connection((HOST, PORT), timeout=120)
        s.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 8192)   # 收得慢，逼服务端阻塞
        req = ("GET /resource?f=%s HTTP/1.1\r\nHost: %s\r\nConnection: close\r\n\r\n"
               % (urllib.parse.quote(path), HOST))
        s.sendall(req.encode())
        total = 0
        while not stop_event.is_set():
            data = s.recv(chunk)
            if not data:
                break
            total += len(data)
            stats[idx] = total
            time.sleep(per_chunk_sleep)
        s.close()
    except Exception:
        pass


def probe(sample_count):
    """一组正常请求，返回延迟列表（秒）。"""
    lat = []
    for _ in range(sample_count):
        url = "http://%s:%d/resource?f=%s" % (HOST, PORT, urllib.parse.quote(PROBE_FILE))
        t0 = time.perf_counter()
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                r.read()
            lat.append(time.perf_counter() - t0)
        except Exception:
            lat.append(-1.0)
    return lat


def summarize(lat):
    good = [x for x in lat if x >= 0]
    if not good:
        return 0.0, 0.0, 0.0, len(lat)
    good.sort()
    return (sum(good) / len(good), good[len(good) // 2], good[-1], len(lat) - len(good))


def main():
    slow_count = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    kbps = int(sys.argv[2]) if len(sys.argv) > 2 else 200
    samples = int(sys.argv[3]) if len(sys.argv) > 3 else 30

    out = io.open(sys.stdout.fileno(), "w", encoding="utf-8", buffering=1, closefd=False)
    out.write("=" * 74 + "\n")
    out.write("慢速客户端阻塞实验\n")
    out.write("=" * 74 + "\n")
    out.write("  慢速下载者 : %d 个，每个限速 %d KB/s，都在拉 %s\n" % (slow_count, kbps, BIG_FILE))
    out.write("  探测请求   : %d 次 %s（正常速度）\n" % (samples, PROBE_FILE))
    out.write("\n")

    out.write("[1] 基线：没有任何慢速下载者\n")
    base = probe(samples)
    b_avg, b_p50, b_max, b_fail = summarize(base)
    out.write("    平均 %.0f ms   P50 %.0f ms   最慢 %.0f ms   失败 %d\n\n" % (
        b_avg * 1000, b_p50 * 1000, b_max * 1000, b_fail))

    out.write("[2] 挂上 %d 个慢速下载者后再探测\n" % slow_count)
    stop = threading.Event()
    stats = [0] * slow_count
    threads = []
    for i in range(slow_count):
        t = threading.Thread(target=slow_reader, args=(stop, BIG_FILE, kbps, stats, i), daemon=True)
        t.start()
        threads.append(t)
    time.sleep(1.5)   # 让慢速请求先占住服务端

    mixed = probe(samples)
    m_avg, m_p50, m_max, m_fail = summarize(mixed)
    stop.set()
    time.sleep(0.3)

    out.write("    平均 %.0f ms   P50 %.0f ms   最慢 %.0f ms   失败 %d\n" % (
        m_avg * 1000, m_p50 * 1000, m_max * 1000, m_fail))
    out.write("    慢速下载者已收到: %s 字节\n\n" % (", ".join(str(x) for x in stats)))

    out.write("-" * 74 + "\n[结论]\n")
    if b_avg > 0:
        ratio = m_avg / b_avg
        out.write("  正常请求平均延迟：%.0f ms  →  %.0f ms （恶化 %.1f 倍）\n" % (
            b_avg * 1000, m_avg * 1000, ratio))
        if ratio >= 5:
            out.write("  → **确认全局阻塞**：%d 个慢速客户端就能把整个资源服务拖住，\n"
                      "     其他人不是「慢一点」，而是被卡到秒级。\n" % slow_count)
        elif ratio >= 2:
            out.write("  → 存在明显相互干扰，慢速客户端会显著拖慢其他人。\n")
        else:
            out.write("  → 本次未观察到明显阻塞（可尝试增大慢速客户端数或降低限速）。\n")
    out.write("\n  说明：真实外网玩家下载 47MB 的大图库（如 Data/ChrSe1l.Lib）时，\n")
    out.write("        以 200KB/s 计需要约 4 分钟，这 4 分钟内其余请求都要排队。\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())

# -*- coding: utf-8 -*-
"""
大文件慢速下载验证（模拟外网小水管）。

背景：
    资源体积跨度极大 —— 音效几百 KB，最大的地砖图库 588MB。
    旧实现用 HttpClient.Timeout 固定 32 秒卡死整个下载，
    588MB 对 2.5MB/s 的外网玩家要 4 分钟，也就是说外网微端在大文件上必然失败。
    现在改成「多久没收到数据」的静默超时，只要数据还在流就不该中断。

本脚本：
    起一个限速 200 KB/s 的资源服务（8MB 文件需要约 40 秒，远超旧实现的 32 秒），
    然后用反射驱动真实 Client.dll 去下载，确认：
      · 下载成功
      · 实际耗时可超过 32 秒（这正是修复点）
      · 落盘字节数正确

用法：python verify_slow_download.py
"""
import os
import shutil
import subprocess
import sys
import threading
import time
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

WORK_ROOT = r'D:\BaiduNetdiskDownload\mir2-20241027'
CLIENT_DIR = os.path.join(WORK_ROOT, 'Build', 'Client', 'Debug')
EXE = os.path.join(WORK_ROOT, 'Tools', 'SlowDownloadTest', 'bin', 'Debug', 'net8.0-windows7.0', 'SlowDownloadTest.exe')
TEST_ROOT = r'D:\mir2_slow_test'
PORT = 5689

BIG_FILE = 'Data/BigTest.Lib'
BIG_SIZE = 8 * 1024 * 1024      # 8 MB
KBPS = 200                      # 限速 200 KB/s -> 约 40 秒
WAIT_MS = 180000                # 客户端等待上限给足

OLD_TIMEOUT_S = 32              # 旧实现的固定超时，用来做对照

received = []


class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def log_message(self, *args):
        pass

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)

        if parsed.path == '/resindex':
            body = (BIG_FILE + '\n').encode('utf-8')
            self.send_response(200)
            self.send_header('Content-Type', 'text/plain; charset=utf-8')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return

        if parsed.path == '/resource':
            query = urllib.parse.parse_qs(parsed.query)
            rel = query.get('f', [''])[0]
            if rel != BIG_FILE:
                self.send_response(404)
                self.send_header('Content-Length', '0')
                self.end_headers()
                return

            received.append(rel)

            self.send_response(200)
            self.send_header('Content-Type', 'application/octet-stream')
            self.send_header('Content-Length', str(BIG_SIZE))
            self.end_headers()

            chunk = b'\x5a' * 8192
            per_chunk = 8192 / 1024.0 / KBPS        # 每块要花的时间
            sent = 0
            try:
                while sent < BIG_SIZE:
                    n = min(len(chunk), BIG_SIZE - sent)
                    self.wfile.write(chunk[:n])
                    self.wfile.flush()
                    sent += n
                    time.sleep(per_chunk)
            except Exception:
                pass
            return

        self.send_response(404)
        self.send_header('Content-Length', '0')
        self.end_headers()


def main():
    if not os.path.exists(EXE):
        print('找不到测试程序，请先编译 Tools/SlowDownloadTest：')
        print('  ' + EXE)
        return 2

    if os.path.isdir(TEST_ROOT):
        shutil.rmtree(TEST_ROOT, ignore_errors=True)

    server = ThreadingHTTPServer(('127.0.0.1', PORT), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()

    host = 'http://127.0.0.1:%d/' % PORT
    print('=' * 72)
    print('大文件慢速下载验证')
    print('=' * 72)
    print('  资源服务  : %s' % host)
    print('  限速      : %d KB/s' % KBPS)
    print('  文件大小  : %.1f MB' % (BIG_SIZE / 1048576.0))
    print('  预计耗时  : 约 %.0f 秒（旧实现固定 %.0f 秒超时，必然失败）' % (
        BIG_SIZE / 1024.0 / KBPS, OLD_TIMEOUT_S))
    print()

    try:
        proc = subprocess.run(
            [EXE, CLIENT_DIR, host, TEST_ROOT, BIG_FILE, str(WAIT_MS)],
            capture_output=True, text=True, timeout=300)
        print(proc.stdout)
        if proc.stderr.strip():
            print('[stderr] ' + proc.stderr)
        code = proc.returncode
    except subprocess.TimeoutExpired:
        print('[失败] 测试程序超时未返回')
        code = 1
    finally:
        server.shutdown()

    print('-' * 72)
    print('[结论]')
    print('  服务端收到的请求: %s' % (received if received else '（无）'))
    if code == 0:
        print('  → 慢速大文件下载成功。下载时长超过了旧实现的固定超时，说明')
        print('     超时已正确改为「静默超时」，不再按总时长误杀大文件。')
    else:
        print('  → 下载失败，需要检查超时逻辑。')
    return code


if __name__ == '__main__':
    sys.exit(main())

# -*- coding: utf-8 -*-
"""
微端「边玩边加载」行为验证（真实 HTTP 服务端 + 真实 Client.dll）。

在服务端一侧起一个与真实资源服务同接口的 HTTP 服务（/resindex、/resource），
记录收到的每一条请求，然后用反射驱动真实的 Client.dll 走一遍
"启动批量登记 -> 游戏中按需取用"的完整流程，最后核对服务端到底收到了什么：

  期望结果：
    - 启动的批量登记阶段（200 个图库登记）      -> 0 次 /resource
    - 真正被用到的资源                          -> 恰好 3 次，且只请求这几个文件
    - 本地和服务端都没有的文件                  -> 连试探请求都不发
    - 资源清单                                  -> 只拉取一次

任何一项不符，说明微端又退化成"一次性全量加载"或"对着不存在文件狂刷 404"。

用法：python verify_microclient_load.py
"""
import os
import shutil
import subprocess
import sys
import threading
import urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

WORK_ROOT = r'D:\BaiduNetdiskDownload\mir2-20241027'
SRV_ROOT = r'D:\mir2_micro_srv'
TEST_ROOT = r'D:\mir2_micro_test'
PORT = 5688
MONSTER_COUNT = 200

# 期望客户端真正去下载的文件（与 MicroTest 用例一一对应）：
#   Data/Monster/000.Lib  按需下载用例
#   Data/Monster/050.Lib  queued 出参用例
#   Data/Monster/001.Lib  阻塞等待用例（模拟进地图）
EXPECTED_RESOURCES = {
    'Data/Monster/000.Lib',
    'Data/Monster/050.Lib',
    'Data/Monster/001.Lib',
}

requests_log = []
log_lock = threading.Lock()


class Handler(BaseHTTPRequestHandler):
    protocol_version = 'HTTP/1.1'

    def log_message(self, *args):
        pass

    def _send(self, code, body=b'', ctype='application/octet-stream'):
        self.send_response(code)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        if body:
            self.wfile.write(body)

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)

        if parsed.path == '/resindex':
            with log_lock:
                requests_log.append(('/resindex', ''))
            lines = []
            for dp, _dn, fn in os.walk(SRV_ROOT):
                rel = os.path.relpath(dp, SRV_ROOT).replace('\\', '/')
                if rel == '.':
                    rel = ''
                for f in fn:
                    lines.append((rel + '/' + f) if rel else f)
            self._send(200, '\n'.join(sorted(lines)).encode('utf-8'),
                       'text/plain; charset=utf-8')
            return

        if parsed.path == '/resource':
            query = urllib.parse.parse_qs(parsed.query)
            rel = query.get('f', [''])[0]
            with log_lock:
                requests_log.append(('/resource', rel))

            full = os.path.join(SRV_ROOT, rel.replace('/', os.sep))
            if os.path.isfile(full):
                with open(full, 'rb') as fh:
                    self._send(200, fh.read())
            else:
                self._send(404)
            return

        self._send(404)


def build_server_resources():
    """造一份"服务端资源"：Data/Monster/000.Lib ~ 199.Lib。"""
    if os.path.isdir(SRV_ROOT):
        shutil.rmtree(SRV_ROOT)

    folder = os.path.join(SRV_ROOT, 'Data', 'Monster')
    os.makedirs(folder, exist_ok=True)

    for i in range(MONSTER_COUNT):
        path = os.path.join(folder, '%03d.Lib' % i)
        # 000.Lib 的内容要与 MicroTest 的校验一致（107 字节、以 'TE' 开头）
        payload = b'TE' + bytes(105)
        with open(path, 'wb') as fh:
            fh.write(payload)


def main():
    print('=' * 68)
    print('微端「边玩边加载」行为验证')
    print('=' * 68)

    build_server_resources()
    print('服务端资源目录 : %s（%d 个图库文件）' % (SRV_ROOT, MONSTER_COUNT))
    print('客户端测试目录 : %s' % TEST_ROOT)

    httpd = ThreadingHTTPServer(('127.0.0.1', PORT), Handler)
    thread = threading.Thread(target=httpd.serve_forever, daemon=True)
    thread.start()
    print('模拟资源服务   : http://127.0.0.1:%d/' % PORT)
    print()

    exe = os.path.join(WORK_ROOT, 'Tools', 'MicroTest', 'bin', 'Debug', 'net8.0-windows7.0', 'MicroTest.exe')
    client_dir = os.path.join(WORK_ROOT, 'Build', 'Client', 'Debug')

    if not os.path.isfile(exe):
        print('找不到 MicroTest.exe，请先编译：', exe)
        httpd.shutdown()
        return 1

    result = subprocess.run(
        [exe, client_dir, TEST_ROOT, 'http://127.0.0.1:%d/' % PORT],
        capture_output=True, text=True, encoding='utf-8', errors='replace')
    print(result.stdout)
    if result.stderr.strip():
        print('[stderr]', result.stderr)

    httpd.shutdown()

    # ---------------- 服务端侧核对 ----------------
    with log_lock:
        index_reqs = [r for p, r in requests_log if p == '/resindex']
        resource_reqs = [r for p, r in requests_log if p == '/resource']

    print('-' * 68)
    print('服务端实际收到的请求')
    print('-' * 68)
    print('  /resindex : %d 次' % len(index_reqs))
    print('  /resource : %d 次' % len(resource_reqs))
    for rel in resource_reqs:
        print('      -> %s' % rel)
    print()

    bad = 0

    def check(name, ok, detail=''):
        nonlocal bad
        if ok:
            print('  [通过] %s' % name)
        else:
            bad += 1
            print('  [失败] %s  %s' % (name, detail))

    # 关键断言 1：批量登记 200 个图库，一个都不许下
    bulk_forbidden = [r for r in resource_reqs if r not in EXPECTED_RESOURCES]
    check('批量登记阶段没有产生任何多余下载',
          len(bulk_forbidden) == 0,
          '多下了 %d 个: %s' % (len(bulk_forbidden), bulk_forbidden[:5]))

    # 关键断言 2：只有真正用到的资源才被请求
    got = set(resource_reqs)
    check('只下载了真正用到的 %d 个资源' % len(EXPECTED_RESOURCES),
          got == EXPECTED_RESOURCES,
          '实际=%s' % sorted(got))

    # 关键断言 3：服务端没有的文件不发试探请求
    check('清单里没有的文件没有发出 HTTP 请求',
          'Data/Monster/999.Lib' not in got,
          '竟然请求了 999.Lib')

    # 关键断言 4：清单只拉一次
    check('资源清单只拉取一次', len(index_reqs) == 1, '实际 %d 次' % len(index_reqs))

    print()
    if result.returncode != 0:
        bad += 1
        print('  [失败] MicroTest 断言未全部通过（退出码 %d）' % result.returncode)

    print('=' * 68)
    if bad == 0:
        print('结论：微端为纯按需加载 —— 启动阶段零下载，资源用到才取，无多余请求。')
    else:
        print('结论：存在 %d 项异常，请检查上面的失败项。' % bad)
    print('=' * 68)
    return 0 if bad == 0 else 2


if __name__ == '__main__':
    sys.exit(main())

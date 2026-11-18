#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
微端下载服务器校验：把 resindex 里每个文件都请求一遍，确认服务器真能下。

用法：
  python verify_micro.py --base http://127.0.0.1:18081 --url-style path
  python verify_micro.py --base http://127.0.0.1:18080 --url-style query

--url-style
  query : GET {base}/resource?f=<percent-encoded>   （客户端现状）
  path  : GET {base}/resource/<percent-encoded>     （路径式，nginx 才能正确解码）
  root  : GET {base}/<percent-encoded>              （资源根=网站根，用于校验 302 的目标服务器）

每个文件用 Range: bytes=0-0 只取 1 字节，避免把 5GB 全拖一遍。
"""
import argparse
import os
import sys
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor

FALLBACK_OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def probe(args, rel):
    enc = urllib.parse.quote(rel, safe="/")
    if args.url_style == "query":
        url = args.base.rstrip("/") + "/resource?f=" + enc
    elif args.url_style == "path":
        url = args.base.rstrip("/") + "/resource/" + enc
    else:   # root：资源根直接就是网站根（302 目标那台服务器的原始形态）
        url = args.base.rstrip("/") + "/" + enc
    req = urllib.request.Request(url, method="GET")
    req.add_header("Range", "bytes=0-0")
    try:
        with FALLBACK_OPENER.open(req, timeout=args.timeout) as r:
            code = r.status
            r.read(1)
            return rel, code, None
    except urllib.error.HTTPError as e:
        return rel, e.code, None
    except Exception as e:
        return rel, 0, str(e)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--index", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "resindex.txt"))
    ap.add_argument("--url-style", choices=["query", "path", "root"], default="path")
    ap.add_argument("--workers", type=int, default=32)
    ap.add_argument("--timeout", type=float, default=30)
    ap.add_argument("--show-fail", type=int, default=60, help="最多列多少个失败项")
    a = ap.parse_args()

    with open(a.index, "r", encoding="utf-8-sig") as f:
        files = [l.strip() for l in f if l.strip()]

    print("清单 %d 条 -> %s  风格=%s" % (len(files), a.base, a.url_style))

    bad, ok, neterr = [], 0, 0
    with ThreadPoolExecutor(max_workers=a.workers) as pool:
        for rel, code, err in pool.map(lambda r: probe(a, r), files):
            if code in (200, 206):
                ok += 1
            elif code == 0:
                neterr += 1
                bad.append((rel, "NET:" + (err or "")))
            else:
                bad.append((rel, str(code)))

    total = len(files)
    print("成功 %d / %d  (%.2f%%)" % (ok, total, ok * 100.0 / total if total else 0))
    if neterr:
        print("网络错误 %d" % neterr)
    if bad:
        print("失败 %d 个%s" % (len(bad), "（列前 %d）" % a.show_fail if len(bad) > a.show_fail else ""))
        for rel, why in bad[:a.show_fail]:
            print("  %-60s %s" % (rel, why))
        ext = {}
        for rel, _ in bad:
            ext[os.path.splitext(rel)[1].lower()] = ext.get(os.path.splitext(rel)[1].lower(), 0) + 1
        print("按扩展名：", sorted(ext.items(), key=lambda x: -x[1]))
    else:
        print("全部通过")
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main())

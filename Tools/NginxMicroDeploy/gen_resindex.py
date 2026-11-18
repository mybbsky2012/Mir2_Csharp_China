#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成微端 /resindex 清单（供 nginx / IIS / 自带服务使用）

规则：
  · 只列资源目录（默认 Data Map Sound），不列客户端本体 dll/exe/配置文件
  · 排除 DirectX/EBWebView（WebView2 运行时缓存，105MB，路径含空格且运行时一直在变）
  · 排除 Screenshots（玩家截图）、*.tmp、*.log、清单自身
  · UTF-8 无 BOM + LF + 正斜杠 —— BOM 会粘在第一条路径前面，导致该文件永远 404

用法：
  python gen_resindex.py                      # 默认 E:\\micromir2 -> ./resindex.txt
  python gen_resindex.py --out D:/Mirserver/ClientResources/resindex.txt
  python gen_resindex.py --include-all        # 除排除项外全量（含根目录文件）
"""
import argparse
import os
import sys

DEFAULT_ROOT = r"D:\Mirserver\ClientResources"
DEFAULT_DIRS = ["Data", "Map", "Sound", "UI-"]
EXCLUDE_DIR_PARTS = ("DirectX", "Screenshots", "__pycache__", ".git", "LibExport")
EXCLUDE_EXT = (".tmp", ".log", ".pdb", ".7z", ".rar", ".zip", ".md")
# .bak / .bak_walklook / .bak_standlegs 一律排除：这些是改图时的本地备份，
# 既不该下发，远端也没有（上一版只排了 ".bak" 精确后缀，漏掉 9 个带后缀的备份文件）
EXCLUDE_SUBSTR = (".bak",)
EXCLUDE_NAMES = ("resindex.txt", "resindex")


def build(root, dirs=None, include_all=False):
    out = []
    root = os.path.abspath(root)
    if not os.path.isdir(root):
        raise SystemExit("资源根目录不存在: " + root)

    if include_all:
        walk_roots = [root]
    else:
        walk_roots = [os.path.join(root, d) for d in (dirs or DEFAULT_DIRS)]
        walk_roots = [w for w in walk_roots if os.path.isdir(w)]
        if not walk_roots:
            raise SystemExit("资源目录一个都不存在: " + ", ".join(dirs or DEFAULT_DIRS))

    for wr in walk_roots:
        for cur, subdirs, files in os.walk(wr):
            subdirs[:] = [s for s in subdirs if s not in EXCLUDE_DIR_PARTS]
            for fn in files:
                if fn in EXCLUDE_NAMES:
                    continue
                low = fn.lower()
                if low.endswith(EXCLUDE_EXT):
                    continue
                if any(s in low for s in EXCLUDE_SUBSTR):
                    continue
                full = os.path.join(cur, fn)
                rel = os.path.relpath(full, root).replace("\\", "/")
                if rel.split("/")[0] in EXCLUDE_DIR_PARTS:
                    continue
                out.append(rel)

    out.sort()
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default=DEFAULT_ROOT)
    ap.add_argument("--out", default=os.path.join(DEFAULT_ROOT, "resindex.txt"))
    ap.add_argument("--dirs", default=",".join(DEFAULT_DIRS))
    ap.add_argument("--include-all", action="store_true")
    a = ap.parse_args()

    lines = build(a.root, [d.strip() for d in a.dirs.split(",") if d.strip()], a.include_all)

    # 关键：编码成 UTF-8 且不要 BOM
    data = ("\n".join(lines) + "\n").encode("utf-8")
    with open(a.out, "wb") as f:
        f.write(data)

    total = sum(os.path.getsize(os.path.join(a.root, l.replace("/", os.sep)))
                for l in lines if os.path.exists(os.path.join(a.root, l.replace("/", os.sep))))
    print("清单写出: " + a.out)
    print("条目数  : %d" % len(lines))
    print("字节数  : %d (%.1f KB)" % (len(data), len(data) / 1024.0))
    print("资源体积: %.2f GB" % (total / 1024.0 / 1024.0 / 1024.0))
    print("首条    : " + (lines[0] if lines else "(空)"))
    print("末条    : " + (lines[-1] if lines else "(空)"))

    # 自查：BOM / CRLF / 空行
    if data[:3] == b"\xef\xbb\xbf":
        print("!! 带 BOM，第一条会 404", file=sys.stderr)
    if b"\r" in data:
        print("!! 含 CR，客户端解析可能带上 \\r", file=sys.stderr)
    if b"\n\n" in data:
        print("!! 含空行", file=sys.stderr)


if __name__ == "__main__":
    main()

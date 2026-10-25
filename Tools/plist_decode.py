#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PList.gz 解码 / 校验 / 生成工具

更新器（Launcher）里显示「列表内容」的那个文件是 PList.gz。
它不是一个文本文件：名字带 .gz，内容却是未经压缩的二进制清单
（AutoPatcherAdmin 里对 PList.gz 有专门跳过压缩的分支）。
直接用记事本打开必然是乱码，这是设计如此，不是文件损坏。

二进制格式（与 Client/Forms/AMain.cs 的 FileInformation 一一对应）：
    int32   条目数 count
    重复 count 次：
        string   文件名（.NET BinaryWriter 的 7-bit 长度前缀 + UTF-8）
        int32    原始长度 Length
        int32    压缩后长度 Compressed（等于 Length 表示没压缩）
        int64    DateTime.ToBinary()，即文件的最后修改时间

用法：
    python plist_decode.py PList.gz                 列出全部条目
    python plist_decode.py PList.gz --check <客户端目录>
                                                    对照本地文件，列出需要重新下载的
    python plist_decode.py PList.gz --csv list.csv  另存为 CSV
    python plist_decode.py --build <客户端目录> PList.gz
                                                    按客户端目录重新生成 PList.gz

只读工具：除了 --build 显式指定输出文件，不会写任何东西。
"""

import argparse
import gzip
import io
import os
import struct
import sys

# --------------------------------------------------------------------------
# 读取
# --------------------------------------------------------------------------


def read_7bit_int(f):
    """读 .NET BinaryWriter 的 7-bit 变长整数（字符串长度前缀）。"""
    result = 0
    shift = 0
    while True:
        b = f.read(1)
        if not b:
            raise EOFError("读取字符串长度时遭遇文件末尾")
        v = b[0]
        result |= (v & 0x7F) << shift
        if (v & 0x80) == 0:
            return result
        shift += 7
        if shift > 35:
            raise ValueError("7-bit 长度字段异常（偏移 %d）" % f.tell())


def read_string(f):
    n = read_7bit_int(f)
    if n == 0:
        return ""
    raw = f.read(n)
    if len(raw) != n:
        raise EOFError("读取字符串内容时遭遇文件末尾")
    return raw.decode("utf-8", errors="replace")


def read_int32(f):
    raw = f.read(4)
    if len(raw) != 4:
        raise EOFError("读取 int32 时遭遇文件末尾")
    return struct.unpack("<i", raw)[0]


def read_int64(f):
    raw = f.read(8)
    if len(raw) != 8:
        raise EOFError("读取 int64 时遭遇文件末尾")
    return struct.unpack("<q", raw)[0]


def from_dotnet_binary(value):
    """DateTime.ToBinary() -> datetime。取不到精确值就返回原始值。"""
    try:
        import datetime

        # .NET 的 Ticks 起点是 0001-01-01，Python 也是，但 .NET 是 100ns 单位。
        ticks = value & 0x3FFFFFFFFFFFFFFF
        if ticks <= 0:
            return None
        epoch = datetime.datetime(1, 1, 1)
        return epoch + datetime.timedelta(microseconds=ticks // 10)
    except Exception:
        return None


class Entry:
    __slots__ = ("name", "length", "compressed", "creation_raw", "creation")

    def __init__(self, name, length, compressed, creation_raw):
        self.name = name
        self.length = length
        self.compressed = compressed
        self.creation_raw = creation_raw
        self.creation = from_dotnet_binary(creation_raw)

    @property
    def is_compressed(self):
        return self.compressed > 0 and self.compressed != self.length


def load_raw(path):
    """读文件，自动识别 gzip（1F 8B）与裸二进制。"""
    with open(path, "rb") as fh:
        data = fh.read()

    if data[:2] == b"\x1f\x8b":
        try:
            data = gzip.decompress(data)
            note = "gzip 容器，已解压"
        except Exception as exc:
            raise SystemExit("看起来是 gzip，但解压失败：%s" % exc)
    else:
        note = "裸二进制（PList.gz 的正常形态）"
    return data, note


def parse(data):
    """解析二进制清单。返回 (entries, 提示信息)。"""
    if data[:1] == b"<":
        raise SystemExit(
            "这个文件是 HTML 而不是清单——多半下载到了 404 页面。\n"
            "检查补丁地址是否写错，或者服务端 /PList.gz 是否存在。"
        )

    if len(data) < 4:
        raise SystemExit("文件太短（%d 字节），不可能是有效的 PList。" % len(data))

    f = io.BytesIO(data)
    count = read_int32(f)

    # 防御：条目数明显超过文件实际能容纳的数量，说明格式对不上
    if count < 0 or count > len(data):
        raise SystemExit(
            "首 4 字节解析出的条目数是 %d，与文件大小 %d 字节不匹配。\n"
            "这通常意味着：① 这不是 PList；② 文件被截断；③ 文件被当文本另存过。" % (count, len(data))
        )

    entries = []
    for i in range(count):
        try:
            name = read_string(f)
            length = read_int32(f)
            compressed = read_int32(f)
            creation = read_int64(f)
        except (EOFError, ValueError) as exc:
            raise SystemExit("第 %d 条记录解析失败（%s），文件可能被截断。" % (i + 1, exc))
        entries.append(Entry(name, length, compressed, creation))

    tail = len(data) - f.tell()
    return entries, tail


# --------------------------------------------------------------------------
# 输出
# --------------------------------------------------------------------------


def human(n):
    for unit in ("B", "KB", "MB", "GB"):
        if n < 1024 or unit == "GB":
            return ("%d %s" % (n, unit)) if unit == "B" else ("%.2f %s" % (n, unit))
        n /= 1024.0


def print_entries(entries):
    print("共 %d 个条目" % len(entries))
    print()
    header = "%-4s %-42s %12s %12s  %-19s" % ("#", "文件名", "大小", "传输长度", "修改时间")
    print(header)
    print("-" * len(header))
    total = 0
    for i, e in enumerate(entries, 1):
        total += e.length
        when = e.creation.strftime("%Y-%m-%d %H:%M:%S") if e.creation else "?"
        flag = "  (压缩)" if e.is_compressed else ""
        print("%-4d %-42s %12s %12s  %-19s%s"
              % (i, e.name, human(e.length), human(e.compressed or e.length), when, flag))
    print("-" * len(header))
    print("合计 %d 个文件，%s" % (len(entries), human(total)))


def check_against(entries, client_dir):
    """对照本地客户端目录，找出会被更新器重新下载的文件。"""
    print("对照目录：%s" % os.path.abspath(client_dir))
    print()

    missing, differ, same = [], [], 0

    for e in entries:
        local = os.path.join(client_dir, e.name.replace("/", os.sep).replace("\\", os.sep))
        if not os.path.isfile(local):
            missing.append(e)
            continue

        st = os.stat(local)
        if st.st_size != e.length:
            differ.append((e, st.st_size, "大小 %s → %s" % (human(st.st_size), human(e.length))))
            continue

        # 修改时间允许 2 秒误差（FAT/网络盘的时间精度只到秒）
        # DateTime.ToBinary() 存的是本地时间刻度，所以这里也用本地时间比对
        if e.creation is not None:
            import datetime

            local_dt = datetime.datetime.fromtimestamp(int(st.st_mtime))
            delta = abs((e.creation - local_dt).total_seconds())
            if delta > 2:
                differ.append((e, st.st_size, "时间 本地 %s ≠ 清单 %s"
                               % (local_dt.strftime("%Y-%m-%d %H:%M:%S"),
                                  e.creation.strftime("%Y-%m-%d %H:%M:%S"))))
                continue

        same += 1

    if missing:
        print("【本地缺失 %d 个】更新时会下载：" % len(missing))
        for e in missing:
            print("   + %-40s %s" % (e.name, human(e.length)))
        print()
    if differ:
        print("【本地已存在但内容/时间不同 %d 个】更新时会覆盖：" % len(differ))
        for e, size, why in differ:
            print("   ~ %-40s %s" % (e.name, why))
        print()
    print("【一致】%d 个，不会重复下载" % same)
    if not missing and not differ:
        print("\n结论：本地与清单完全一致，更新器不会下载任何文件。")


def dump_csv(entries, path):
    import csv

    with open(path, "w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["序号", "文件名", "大小", "传输长度", "是否压缩", "修改时间"])
        for i, e in enumerate(entries, 1):
            w.writerow([i, e.name, e.length, e.compressed or e.length, "是" if e.is_compressed else "否",
                        e.creation.strftime("%Y-%m-%d %H:%M:%S") if e.creation else ""])
    print("已写出 CSV：%s（%d 行）" % (path, len(entries)))


# --------------------------------------------------------------------------
# 生成
# --------------------------------------------------------------------------


def write_7bit_int(out, value):
    while value >= 0x80:
        out.write(bytes([(value & 0x7F) | 0x80]))
        value >>= 7
    out.write(bytes([value]))


def datetime_to_binary(dt):
    import datetime

    if dt is None:
        dt = datetime.datetime(1980, 1, 1)
    delta = dt - datetime.datetime(1, 1, 1)
    ticks = int(delta.total_seconds() * 10_000_000)
    return ticks & 0x3FFFFFFFFFFFFFFF


def build(client_dir, out_path, compress=False):
    """按客户端目录生成 PList.gz（默认与 AutoPatcherAdmin 一致：不压缩内容）。"""
    import datetime
    import zlib

    files = []
    for root, _dirs, names in os.walk(client_dir):
        for n in names:
            full = os.path.join(root, n)
            rel = os.path.relpath(full, client_dir).replace(os.sep, "\\")
            files.append((rel, full))

    files.sort(key=lambda x: x[0].lower())

    body = io.BytesIO()
    for rel, full in files:
        st = os.stat(full)
        length = st.st_size
        if compress:
            with open(full, "rb") as fh:
                payload = zlib.compress(fh.read(), 9)
            transferred = len(payload)
        else:
            transferred = length

        body.write(bytes())  # noop, 保持结构清晰
        write_7bit_int(body, len(rel.encode("utf-8")))
        body.write(rel.encode("utf-8"))
        body.write(struct.pack("<i", length))
        body.write(struct.pack("<i", transferred))
        body.write(struct.pack("<q", datetime_to_binary(
            datetime.datetime.fromtimestamp(int(st.st_mtime)))))

    head = struct.pack("<i", len(files))
    data = head + body.getvalue()

    with open(out_path, "wb") as fh:
        fh.write(data)

    print("已生成 %s：%d 个文件，%s" % (out_path, len(files), human(len(data))))
    if compress:
        print("注意：compressed=True 时还需把每个文件打成 .gz 一并上传，否则客户端下载的是原件。")
    else:
        print("与 AutoPatcherAdmin 的默认输出一致（内容不压缩，仅文件名带 .gz）。")


# --------------------------------------------------------------------------


def main():
    ap = argparse.ArgumentParser(description="PList.gz 解码 / 校验 / 生成")
    ap.add_argument("path", nargs="?", help="PList.gz 或解压后的清单文件")
    ap.add_argument("--check", metavar="CLIENT_DIR", help="对照本地客户端目录，列出会被重新下载的文件")
    ap.add_argument("--csv", metavar="CSV", help="把清单另存为 CSV")
    ap.add_argument("--build", metavar="CLIENT_DIR", help="按客户端目录重新生成清单（配合 path 作为输出文件）")
    ap.add_argument("--compress", action="store_true", help="--build 时标记为压缩传输（默认不压缩）")
    args = ap.parse_args()

    if args.build:
        if not args.path:
            ap.error("--build 需要同时给出输出文件名，例如：--build E:\\micromir2 PList.gz")
        if not os.path.isdir(args.build):
            ap.error("目录不存在：%s" % args.build)
        build(args.build, args.path, args.compress)
        return

    if not args.path:
        ap.error("请给出 PList.gz 的路径")

    if not os.path.isfile(args.path):
        raise SystemExit("文件不存在：%s" % args.path)

    data, note = load_raw(args.path)
    entries, tail = parse(data)

    print("文件：%s（%d 字节，%s）" % (args.path, len(data), note))
    print()

    if args.check:
        check_against(entries, args.check)
    else:
        print_entries(entries)
        if tail:
            print("\n提示：解析结束后还剩 %d 字节未用掉，"
                  "说明清单尾部有额外数据（不影响更新器读取）。" % tail)

    if args.csv:
        print()
        dump_csv(entries, args.csv)


if __name__ == "__main__":
    main()

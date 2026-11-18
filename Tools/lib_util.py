"""Mir2 .Lib 图库读取工具（只读）。

格式（与 Client/MirGraphics/MLibrary.cs、Tools/HumUpLook 一致）：
    int32 version
    int32 count
    [int32 frameSeek]        仅 version >= 3
    int32 offsets[count]     每帧在文件中的绝对偏移，<=0 表示空帧
帧头 17 字节：
    int16 w, h, fx, fy, ?, ?, uint8 ?, int32 length
帧数据：
    gzip 压缩的 BGRA 像素，长度 = w*h*4
"""

import gzip
import struct

from PIL import Image

_cache = {}


def load(path):
    """读取并缓存一个 .Lib，返回 (bytes, offsets)。"""
    key = str(path)
    if key in _cache:
        return _cache[key]

    data = open(key, "rb").read()
    version, count = struct.unpack_from("<ii", data, 0)
    pos = 16 if version >= 3 else 8
    offsets = struct.unpack_from("<%di" % count, data, pos)
    _cache[key] = (data, offsets)
    return data, offsets


def count(path):
    return len(load(path)[1])


def frame(path, index):
    """取一帧，返回 (RGBA Image, fx, fy)；空帧/越界返回 None。"""
    data, offsets = load(path)
    if index < 0 or index >= len(offsets):
        return None

    off = offsets[index]
    if off <= 0 or off + 17 > len(data):
        return None

    w, h, fx, fy, _, _, _, length = struct.unpack_from("<hhhhhhBi", data, off)
    if w <= 0 or h <= 0 or length <= 0:
        return None

    start = off + 17
    end = start + length
    if end > len(data):
        return None

    try:
        raw = gzip.decompress(data[start:end])
    except Exception:
        return None

    if len(raw) < w * h * 4:
        return None

    img = Image.frombytes("RGBA", (w, h), raw[: w * h * 4], "raw", "BGRA")
    return img, fx, fy


def is_empty(path, index):
    return frame(path, index) is None


def info(path):
    data, offsets = load(path)
    version, cnt = struct.unpack_from("<ii", data, 0)
    real = sum(1 for o in offsets if o > 0)
    return {"version": version, "count": cnt, "frames": real}

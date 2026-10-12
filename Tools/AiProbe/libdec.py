# -*- coding: utf-8 -*-
"""
Crystal .Lib 解码器（只读）。
格式：int32 版本 / int32 帧数 / [v3+] int32 帧段偏移 / N×int32 数据偏移
每帧：int16 W,H,X,Y,ShadowX,ShadowY + byte Shadow + int32 Length + gzip(BGRA, W*H*4)
"""
import os
import gzip
import struct
from PIL import Image

BGRA = "BGRA"


class LibImage:
    __slots__ = ("w", "h", "x", "y", "shadow_x", "shadow_y", "shadow", "data")

    def __init__(self, w, h, x, y, sx, sy, sh, data):
        self.w, self.h, self.x, self.y = w, h, x, y
        self.shadow_x, self.shadow_y, self.shadow = sx, sy, sh
        self.data = data  # BGRA bytes

    def to_image(self, scale=1):
        if self.w <= 0 or self.h <= 0:
            return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
        img = Image.frombytes("RGBA", (self.w, self.h), self.data, "raw", BGRA)
        if scale != 1:
            img = img.resize((self.w * scale, self.h * scale), Image.NEAREST)
        return img


class Lib:
    def __init__(self, path):
        self.path = path
        self.images = []
        self.version = 0
        self.frames = {}   # MirAction -> (start, count, offset, delay)
        with open(path, "rb") as f:
            raw = f.read()
        if len(raw) < 12:
            raise ValueError("文件过小")
        ver, count = struct.unpack_from("<ii", raw, 0)
        self.version = ver
        pos = 8
        if ver >= 3:
            frame_seek, = struct.unpack_from("<i", raw, pos)
            pos += 4
        else:
            frame_seek = 0
        offsets = list(struct.unpack_from("<%di" % count, raw, pos))
        self.raw = raw
        self.offsets = offsets
        self.frame_seek = frame_seek

    def count(self):
        return len(self.offsets)

    def _read_at(self, off):
        raw = self.raw
        w, h, x, y, sx, sy = struct.unpack_from("<6h", raw, off)
        sh = raw[off + 12]
        (length,) = struct.unpack_from("<i", raw, off + 13)
        blob = raw[off + 17: off + 17 + length]
        try:
            data = gzip.decompress(blob)
        except Exception:
            data = b""
        expected = w * h * 4
        if len(data) < expected:
            data = data + b"\x00" * (expected - len(data))
        return LibImage(w, h, x, y, sx, sy, sh, data[:expected])

    def get(self, index):
        if index < 0 or index >= len(self.offsets):
            return None
        if self.images and self.images[index] is not None:
            return self.images[index]
        if not self.images:
            self.images = [None] * len(self.offsets)
        img = self._read_at(self.offsets[index])
        self.images[index] = img
        return img

    def read_frames(self):
        """v3 图库的帧段：MirAction(byte) + 8×int32 + 2×bool = 1+34 字节"""
        if self.version < 3 or self.frame_seek <= 0:
            return {}
        raw = self.raw
        if self.frame_seek + 4 > len(raw):
            return {}
        (fc,) = struct.unpack_from("<i", raw, self.frame_seek)
        p = self.frame_seek + 4
        out = {}
        for _ in range(fc):
            if p + 35 > len(raw):
                break
            action = raw[p]
            start, cnt, skip, interval = struct.unpack_from("<4i", raw, p + 1)
            reverse = raw[p + 33]
            blend = raw[p + 34]
            p += 35
            out[action] = (start, cnt, skip, interval, bool(reverse), bool(blend))
        return out

    def best_static_frame(self):
        """挑一帧用来当缩略图：优先站立动作(0)，否则第 0 帧。"""
        fr = self.read_frames()
        if 0 in fr:
            start, cnt, skip, _iv, _rv, _bl = fr[0]
            return start + max(0, skip)
        for i in range(min(4, len(self.offsets))):
            im = self.get(i)
            if im and im.w > 2 and im.h > 2:
                return i
        return 0


def load_dir(d):
    out = {}
    for name in os.listdir(d):
        if not name.lower().endswith(".lib"):
            continue
        stem = name[:-4]
        if not stem.isdigit():
            continue
        try:
            out[int(stem)] = os.path.join(d, name)
        except ValueError:
            pass
    return out


if __name__ == "__main__":
    import sys
    d = r"E:\BaiduMir2Monster"
    libs = load_dir(r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\data\monster")
    print("图库文件数:", len(libs))
    idx = int(sys.argv[1]) if len(sys.argv) > 1 else 508
    lb = Lib(libs[idx])
    print(f"{idx}.Lib 版本={lb.version} 帧数={lb.count()} 帧段={len(lb.read_frames())}")
    fr = lb.read_frames()
    print("帧段(动作->start,count,off,delay):", fr)
    fi = lb.best_static_frame()
    print("选用帧:", fi)
    im = lb.get(fi)
    print("尺寸:", im.w, im.h, "偏移:", im.x, im.y)
    out = r"D:\mir2-20241027\Tools\AiProbe\test_%d.png" % idx
    im.to_image(2).save(out)
    print("已写出", out)

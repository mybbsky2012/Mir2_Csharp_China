# -*- coding: utf-8 -*-
"""
EuRe 资源库解密/提取工具
格式（来自 ClientVortice.dll 反编译 MLibrary/MImage，LibVersion=4）:
  文件头: int32 Version(4=明文, 4000=Protected加密) + int32 Count
  末尾索引: Count x 12B (Position, Length, MaskLength)
  每图: int16 Width, int16 Height(^Width异或), int16 X, int16 Y,
        int16 ShadowX, int16 ShadowY, byte Shadow(bit7=有Mask)
        之后 gzip 的 BGRA 像素 (W*H*4)
用法: python EuReExtract.py <目录或文件> [输出目录]
"""
import io
import os
import struct
import sys
import zlib

from PIL import Image


def decode(data):
    """返回 [(index, w, h, x, y, shadow_x, shadow_y, shadow, has_mask, bgra或None, mask或None), ...]"""
    ver, count = struct.unpack_from('<ii', data, 0)
    if ver not in (4, 4000):
        raise ValueError(f'不支持的版本 {ver}')
    protected = (ver == 4000)
    if protected:
        raise ValueError('该库是 4000 加密版，本工具不支持')
    idx_off = len(data) - count * 12
    out = []
    for i in range(count):
        pos, length, mask_len = struct.unpack_from('<iii', data, idx_off + i * 12)
        if pos <= 0 or pos >= len(data):
            out.append((i, 0, 0, 0, 0, 0, 0, 0, False, None, None))
            continue
        w, h_hx, x, y, sx, sy = struct.unpack_from('<hhhhhh', data, pos)
        h = h_hx ^ w
        shadow = data[pos + 12]
        has_mask = (shadow >> 7) == 1
        img = mask = None
        if w > 0 and h > 0:
            try:
                img = gzip_dec(data, pos + 13, length)
                if len(img) != w * h * 4:
                    img = None  # 尺寸不符则放弃该图
            except Exception:
                img = None
        out.append((i, w, h, x, y, sx, sy, shadow, has_mask, img, mask))
    return out


def gzip_dec(data, off, length):
    d = zlib.decompressobj(16 + zlib.MAX_WBITS)
    return d.decompress(data[off:off + length])


def save_png(path, w, h, bgra):
    im = Image.frombytes('RGBA', (w, h), bgra, 'raw', 'BGRA')
    im.save(path)


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else '.'
    out_root = sys.argv[2] if len(sys.argv) > 2 else None
    files = []
    if os.path.isdir(src):
        files = [os.path.join(src, f) for f in sorted(os.listdir(src))
                 if f.lower().endswith('.eure')]
    else:
        files = [src]
    if out_root is None:
        out_root = os.path.join(src if os.path.isdir(src) else os.path.dirname(src),
                                '解密输出')
    os.makedirs(out_root, exist_ok=True)

    for fn in files:
        name = os.path.splitext(os.path.basename(fn))[0]
        out_dir = os.path.join(out_root, name)
        os.makedirs(out_dir, exist_ok=True)
        data = open(fn, 'rb').read()
        try:
            images = decode(data)
        except Exception as ex:
            print(f'[跳过] {fn}: {ex}')
            continue
        ok = empty = bad = 0
        meta = []
        for (i, w, h, x, y, sx, sy, shadow, has_mask, bgra, mask) in images:
            meta.append(f'{i}\t{w}\t{h}\t{x}\t{y}\t{sx}\t{sy}\t{shadow}')
            if bgra is None:
                if w == 0 or h == 0:
                    empty += 1
                else:
                    bad += 1
                continue
            save_png(os.path.join(out_dir, f'{i}.png'), w, h, bgra)
            ok += 1
        with io.open(os.path.join(out_dir, '_偏移.txt'), 'w', encoding='utf-8') as f:
            f.write('# 索引\t宽\t高\tX偏移\tY偏移\tShadowX\tShadowY\tShadow\n')
            f.write('\n'.join(meta) + '\n')
        print(f'[完成] {os.path.basename(fn)}: 共 {len(images)} 图, '
              f'导出 {ok}, 空图 {empty}, 异常 {bad} -> {out_dir}')


if __name__ == '__main__':
    main()

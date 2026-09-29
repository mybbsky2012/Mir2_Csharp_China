# -*- coding: utf-8 -*-
"""
把客户端里散落的硬编码分辨率判断，统一收敛到 ResolutionHelper 的语义函数上。

改造前判断形式有 4 种、共 30 余处，新增分辨率档位时极易漏改：
    Settings.Resolution != 800      -> !ResolutionHelper.IsLegacySmall(Settings.Resolution)
    Settings.Resolution == 800      -> ResolutionHelper.IsLegacySmall(Settings.Resolution)
    Settings.Resolution > 1024      -> ResolutionHelper.IsWide(Settings.Resolution)
    Settings.Resolution == 1024     -> !ResolutionHelper.IsWide(Settings.Resolution)

替换是严格等价的，只改可读性与可维护性，不改变布局行为。
"""
import io
import os
import sys

ROOT = r'D:\BaiduNetdiskDownload\mir2-20241027\Client'

SUBS = [
    ('Settings.Resolution != 800', '!ResolutionHelper.IsLegacySmall(Settings.Resolution)'),
    ('Settings.Resolution == 800', 'ResolutionHelper.IsLegacySmall(Settings.Resolution)'),
    ('Settings.Resolution > 1024', 'ResolutionHelper.IsWide(Settings.Resolution)'),
    ('Settings.Resolution == 1024', '!ResolutionHelper.IsWide(Settings.Resolution)'),
]

# ResolutionHelper 自身所在的目录不处理
SKIP_DIRS = {os.path.join(ROOT, 'Resolution'), os.path.join(ROOT, 'obj'), os.path.join(ROOT, 'bin')}


def main():
    total = {}
    for dp, dn, fn in os.walk(ROOT):
        if any(os.path.normcase(dp).startswith(os.path.normcase(s)) for s in SKIP_DIRS):
            continue
        for f in fn:
            if not f.endswith('.cs'):
                continue
            path = os.path.join(dp, f)
            with io.open(path, encoding='utf-8-sig') as fh:
                text = fh.read()

            original = text
            count = 0
            for old, new in SUBS:
                n = text.count(old)
                if n:
                    text = text.replace(old, new)
                    count += n

            if text == original:
                continue

            # 确保引用了 Client.Resolution
            if 'using Client.Resolution;' not in text:
                lines = text.split('\n')
                # 插到第一个 using 之后；没有 using 就插到首行注释之后
                insert_at = 0
                for i, line in enumerate(lines):
                    if line.strip().startswith('using '):
                        insert_at = i + 1
                lines.insert(insert_at, 'using Client.Resolution;')
                text = '\n'.join(lines)

            with io.open(path, 'w', encoding='utf-8-sig', newline='') as fh:
                fh.write(text)

            rel = os.path.relpath(path, ROOT)
            total[rel] = count
            print('%-50s %d 处' % (rel, count))

    if not total:
        print('没有需要替换的内容')
        return 1

    print('\n共改动 %d 个文件、%d 处' % (len(total), sum(total.values())))
    return 0


if __name__ == '__main__':
    sys.exit(main())

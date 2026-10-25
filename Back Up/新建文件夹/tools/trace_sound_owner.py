# -*- coding: utf-8 -*-
"""查某个怪物的 .lst 指向文件，和资源包里哪些「数字命名文件」内容完全相同，
   借内容相同的文件确定这些音效到底属于哪个图档号。"""
import os, hashlib, collections, re

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
LST = r"E:\micromir2\Sound\SoundList.lst"

md5 = {}
for f in os.listdir(D):
    if f.lower().endswith('.wav') and os.path.getsize(os.path.join(D, f)) > 100:
        md5[f] = hashlib.md5(open(os.path.join(D, f), 'rb').read()).hexdigest()
h2f = collections.defaultdict(list)
for f, h in md5.items():
    h2f[h].append(f)

lst = {}
for ln in open(LST, encoding='utf-8', errors='replace').read().splitlines():
    p = ln.replace(' ', '').replace('\t', ':').split(':')
    if len(p) <= 1:
        continue
    try:
        i = int(p[0])
    except ValueError:
        continue
    if i not in lst:
        lst[i] = p[-1]

num = re.compile(r'^(\d{3,4})-(\d+)\.wav$', re.I)

for M, label in [(20, '骷髅'), (25, '多钩猫'), (26, '钉耙猫'), (27, '稻草人'), (10, '食人花'), (30, '沃玛战士')]:
    print("=" * 74)
    print("【%s】图档 %d" % (label, M))
    print("  .lst 指向：")
    for o in range(6):
        f = lst.get(M * 10 + o)
        if not f:
            continue
        mates = [x for x in h2f.get(md5.get(f, ''), []) if x != f]
        nums = sorted(set('%s(图档%s 偏移%s)' % (x, m.group(1), m.group(2))
                          for x in mates if (m := num.match(x))))
        desc = sorted(x for x in mates if not num.match(x))
        line = "    +%d -> %-22s" % (o, f)
        if nums:
            line += " 内容同: " + ', '.join(nums[:5])
        if desc:
            line += " 同名内容: " + ', '.join(desc[:3])
        print(line)

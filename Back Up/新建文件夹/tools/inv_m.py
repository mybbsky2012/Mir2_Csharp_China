import os, re, collections
for label, C in (("客户端", r"E:\micromir2\Sound"),
                 ("服务端", r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound")):
    groups = collections.defaultdict(list)
    for f in os.listdir(C):
        m = re.match(r'^M(\d+)-(\d+)\.(wav|mp3)$', f, re.I)
        if m:
            groups[int(m.group(1))].append(f)
    print(f"===== {label} =====")
    for k in sorted(groups):
        print(f"M{k:4d}: {sorted(groups[k])}")
    print()

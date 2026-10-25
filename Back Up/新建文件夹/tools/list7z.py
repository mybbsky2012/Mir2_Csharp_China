import py7zr, re
BS = chr(92)
paths = [
 r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\服务端和源代码\10月5日的代码和服务器端\假人客户端文件.7z",
 r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\服务端和源代码\10月5日的代码和服务器端\20261005-假人版Mir2Server.7z",
 r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\服务端和源代码\9月30日的代码和服务器端\20260930Mir2Server.7z",
 r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\服务端和源代码\微端micromir2.7z",
]
def norm(n):
    return n.replace(BS, "/")
for p in paths:
    print("="*70)
    print("ARCHIVE:", p.split(BS)[-1])
    try:
        with py7zr.SevenZipFile(p, 'r') as z:
            names = [norm(n) for n in z.getnames()]
            print("total entries:", len(names))
            snd = [n for n in names if "/Sound/" in n or n.lower().startswith("sound/")]
            print("Sound entries:", len(snd))
            m6 = sorted(set(n for n in snd if re.search(r'M6\d-', n, re.I)))
            m7 = sorted(set(n for n in snd if re.search(r'M7\d-', n, re.I)))
            print("M6x:", m6[:30])
            print("M7x:", m7[:30])
            lsts = [n for n in names if n.lower().endswith(('.lst','.lst.bak')) and 'ound' in n.lower()]
            print("lst:", lsts[:10])
            # 顺便看前若干条目
            print("sample:", names[:8])
    except Exception as e:
        print("ERR:", type(e).__name__, e)

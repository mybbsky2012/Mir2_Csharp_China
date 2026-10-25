import py7zr, os
BS = chr(92)
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\服务端和源代码\微端micromir2.7z"
out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\_work\micromir2_sound"
os.makedirs(out, exist_ok=True)
with py7zr.SevenZipFile(p, 'r') as z:
    names = z.getnames()
    sound = [n for n in names if n.replace(BS,"/").startswith("Sound/")]
    print("Sound entries:", len(sound))
    for n in sorted(sound): print("  ", n)
    z.extract(path=out, targets=sound)
print("extracted to", out)

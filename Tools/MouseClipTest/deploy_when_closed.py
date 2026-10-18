"""等客户端退出后自动部署新 Client.dll（客户端运行时 dll 被占用，无法覆盖）。"""
import os
import shutil
import subprocess
import time

SRC = r"D:\mir2-20241027\Build\Client\Release\Client.dll"
DSTS = [
    r"E:\micromir2\Client.dll",
    r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Client.dll",
]
LOG = r"D:\mir2-20241027\Tools\MouseClipTest\deploy_wait.log"


def client_running():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Client.exe", "/FO", "CSV", "/NH"],
                         capture_output=True, text=True).stdout
    return "Client.exe" in out


def log(msg):
    with open(LOG, "a", encoding="utf-8") as f:
        f.write("[%s] %s\n" % (time.strftime("%H:%M:%S"), msg))


log("等待客户端退出…")
deadline = time.time() + 3600
while time.time() < deadline:
    if not client_running():
        break
    time.sleep(3)
else:
    log("超时 1 小时，放弃")
    raise SystemExit(0)

log("客户端已退出，开始部署")
time.sleep(2)

for dst in DSTS:
    for attempt in range(30):
        try:
            shutil.copy2(SRC, dst)
            log("OK  %s" % dst)
            break
        except Exception as e:
            if attempt == 29:
                log("FAIL %s : %r" % (dst, e))
            else:
                time.sleep(2)

log("完成；源文件 MD5 见上，可再校验")

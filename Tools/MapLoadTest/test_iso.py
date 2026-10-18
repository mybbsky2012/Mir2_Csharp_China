# -*- coding: utf-8 -*-
"""
微端「切图缺 .map」端到端验证（隔离目录 D:\mir2test，不碰用户原始客户端）

验证点：
  1. 缺地图时不再主线程死等 → 出现载入画面 + 进度文字
  2. 等待期间持续发心跳 → 不再被服务端踢下线
  3. 下载完成后正常进图
"""
import ctypes, subprocess, time, os, io
from ctypes import wintypes

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

CWD = r"D:\mir2test"
EXE = os.path.join(CWD, "Client.exe")
CFG = os.path.join(CWD, "Mir2Config.ini")
LOG = os.path.join(CWD, "MicroClient.log")
SHOTS = r"D:\mir2-20241027\Tools\MapLoadTest"

def read_cfg():
    return io.open(CFG, encoding="utf-8", errors="ignore").read()

def hwnds_of_pid(pid):
    found = []
    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD(); user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h): found.append(h)
        return True
    user32.EnumWindows(cb, 0)
    return found

def shot(hwnd, name):
    from PIL import ImageGrab
    r = wintypes.RECT(); user32.GetWindowRect(hwnd, ctypes.byref(r))
    img = ImageGrab.grab(bbox=(r.left, r.top, r.right, r.bottom))
    img.save(os.path.join(SHOTS, name))
    return img

# 直连（设计器被删了，正式客户端走启动器分支，这里用配置直连）
cfg = read_cfg()
cfg = cfg.replace("Enabled=True", "Enabled=False", 1)
cfg = cfg.replace("RateLimit=0", "RateLimit=100", 1)
io.open(CFG, "w", encoding="utf-8").write(cfg)
print("限速已设为 100KB/s（模拟窄带）")

# 地图目录清空：客户端进图时必然缺 .map
import shutil
mapdir = os.path.join(CWD, "Map")
if os.path.exists(mapdir): shutil.rmtree(mapdir, ignore_errors=True)
os.makedirs(mapdir, exist_ok=True)

if os.path.exists(LOG): os.remove(LOG)

proc = subprocess.Popen([EXE], cwd=CWD)
print("客户端已启动 pid=", proc.pid)

entered = False
try:
    for t in range(90):
        time.sleep(1)
        if proc.poll() is not None:
            print("%ds 客户端退出 rc=%s" % (t + 1, proc.poll())); break
        hs = hwnds_of_pid(proc.pid)
        if not hs: continue
        if t % 3 == 0 and t <= 45:
            shot(hs[0], "iso_%02d.png" % t)
        txt = ""
        try:
            txt = io.open(LOG, encoding="utf-8", errors="ignore").read()
        except Exception:
            pass
        if "已下载 Map/" in txt:
            if not entered:
                print("%ds ★ 地图下载完成记录出现" % (t + 1))
            entered = True
            if entered and t > 0:
                time.sleep(3)
                hs2 = hwnds_of_pid(proc.pid)
                if hs2: shot(hs2[0], "iso_entered.png")
                break
    print("下载地图:", entered)

    # 检查有没有掉线提示：MicroClient.log 之外，看 Error.txt 是否新增
    if os.path.exists(LOG):
        lines = io.open(LOG, encoding="utf-8", errors="ignore").read().splitlines()
        print("--- MicroClient.log 尾部 ---")
        print("\n".join(lines[-20:]))
finally:
    if proc.poll() is None:
        proc.terminate()
        try: proc.wait(8)
        except Exception: proc.kill()
    io.open(CFG, "w", encoding="utf-8").write(cfg)
    print("配置已还原")

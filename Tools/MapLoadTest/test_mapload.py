# -*- coding: utf-8 -*-
"""
微端地图加载 E2E 验证：
1) 把本地 Map 目录临时改名 → 客户端进游戏必然缺 .map → 触发「下载中保心跳」新逻辑
2) 观察是否正常进入游戏（不再掉线/卡死），MicroClient.log 里应有「已下载 Map/xxx.map」
3) 结束后恢复 Map 目录与配置
"""
import ctypes, subprocess, time, os, shutil, sys
from ctypes import wintypes

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

CWD = r"E:\micromir2"
EXE = os.path.join(CWD, "Client.exe")
CFG = os.path.join(CWD, "Mir2Config.ini")
LOG = os.path.join(CWD, "MicroClient.log")
MAPDIR = os.path.join(CWD, "Map")
MAPBAK = os.path.join(CWD, "Map_testbak")
SHOTS = r"D:\mir2-20241027\Tools\MapLoadTest"

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
    ImageGrab.grab(bbox=(r.left, r.top, r.right, r.bottom)).save(os.path.join(SHOTS, name))

cfg_backup = open(CFG, encoding="utf-8", errors="ignore").read()
log_backup = open(LOG, encoding="utf-8", errors="ignore").read() if os.path.exists(LOG) else ""
proc = None
try:
    # 1) 准备：直连 + 缺地图
    open(CFG, "w", encoding="utf-8").write(cfg_backup.replace("Enabled=True", "Enabled=False", 1))
    if os.path.exists(MAPDIR):
        os.rename(MAPDIR, MAPBAK)
        print("已临时移走 Map 目录（%d 个文件）" % len(os.listdir(MAPBAK)))
    if os.path.exists(LOG): os.remove(LOG)

    # 2) 启动
    proc = subprocess.Popen([EXE], cwd=CWD)
    downloaded = False
    for t in range(75):
        time.sleep(2)
        rc = proc.poll()
        if rc is not None:
            print("%ds 客户端退出 rc=%s" % (t*2, rc)); break
        hs = hwnds_of_pid(proc.pid)
        if hs and t % 2 == 0:
            shot(hs[0], "e2e_%03d.png" % t)
        try:
            txt = open(LOG, encoding="utf-8", errors="ignore").read()
            if "Map/" in txt and "已下载" in txt:
                downloaded = True
                print("%ds 日志出现地图下载记录" % (t*2))
                # 再多截两帧确认进图
                time.sleep(4)
                hs = hwnds_of_pid(proc.pid)
                if hs: shot(hs[0], "e2e_final.png")
                break
        except FileNotFoundError:
            pass
    else:
        print("75x2 秒内未出现地图下载记录")

    print("地图已下载:", downloaded)
    if os.path.exists(LOG):
        tail = open(LOG, encoding="utf-8", errors="ignore").read()
        print("--- MicroClient.log 尾部 ---")
        print("\n".join(tail.splitlines()[-25:]))
    print("进程状态:", "运行中" if proc.poll() is None else "已退出")
finally:
    # 3) 清理
    if proc and proc.poll() is None:
        proc.terminate()
        try: proc.wait(8)
        except Exception: proc.kill()
    if os.path.exists(MAPBAK):
        if os.path.exists(MAPDIR):
            shutil.rmtree(MAPDIR, ignore_errors=True)   # 只删测试期间新下载的
        os.rename(MAPBAK, MAPDIR)
        print("Map 目录已还原")
    open(CFG, "w", encoding="utf-8").write(cfg_backup)
    # 测试日志恢复原样
    open(LOG, "w", encoding="utf-8").write(log_backup)
    print("配置已还原")

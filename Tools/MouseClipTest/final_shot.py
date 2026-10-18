import ctypes, subprocess, time
from ctypes import wintypes
user32 = ctypes.windll.user32; kernel32 = ctypes.windll.kernel32
user32.SetProcessDPIAware()
CLIENT = r"E:/micromir2/Client.exe"; CWD = r"E:/micromir2"

def hwnd_of_pid(pid):
    found = []
    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD(); user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h): found.append(h)
        return True
    user32.EnumWindows(cb, 0)
    return found[0] if found else None

proc = subprocess.Popen([CLIENT], cwd=CWD)
hwnd = None
for _ in range(60):
    time.sleep(0.5)
    hwnd = hwnd_of_pid(proc.pid)
    if hwnd: break
time.sleep(30)  # 等地图/资源加载完
cr = wintypes.RECT(); user32.GetClientRect(hwnd, ctypes.byref(cr))
print("client", (cr.right, cr.bottom))
from PIL import ImageGrab
ImageGrab.grab().save(r"D:/mir2-20241027/Tools/MouseClipTest/F_game_final.png")
proc.terminate()

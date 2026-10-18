"""验证命中区是否与可见像素对齐（登录界面 AccountButton）。
预期：视觉区 = 客户区 (298,354)-(396,377)；旧命中框 (297,353)-(394,375)。
  P1 (395,375)：视觉内、旧框外 -> 修复后应 hover
  P2 (294,350)：旧框内、视觉外 -> 修复后不应 hover
  CENTER      ：对照组，应 hover
"""
import ctypes
import subprocess
import time
from ctypes import wintypes

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32
user32.SetProcessDPIAware()

CLIENT = r"E:\micromir2\Client.exe"
CWD = r"E:\micromir2"
OUT = r"D:\mir2-20241027\Tools\MouseClipTest"

# LoginDialog TrueSize(1084)=326x220 -> (237,190)；AccountButton (60,163) -> 客户区 (297,353)
BTN_CLIENT = (297, 353)
BTN_SIZE = (100, 25)


def hwnd_of_pid(pid):
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h):
            found.append(h)
        return True

    user32.EnumWindows(cb, 0)
    return found[0] if found else None


def force_fg(hwnd):
    tid_self = kernel32.GetCurrentThreadId()
    tid_t = user32.GetWindowThreadProcessId(hwnd, None)
    fg = user32.GetForegroundWindow()
    tid_fg = user32.GetWindowThreadProcessId(fg, None)
    user32.AttachThreadInput(tid_self, tid_fg, True)
    user32.AttachThreadInput(tid_self, tid_t, True)
    user32.BringWindowToTop(hwnd)
    user32.SetForegroundWindow(hwnd)
    user32.SetFocus(hwnd)
    user32.AttachThreadInput(tid_self, tid_fg, False)
    user32.AttachThreadInput(tid_self, tid_t, False)
    time.sleep(0.4)
    return user32.GetForegroundWindow() == hwnd


def client_to_screen(hwnd, x, y):
    pt = wintypes.POINT(x, y)
    user32.ClientToScreen(hwnd, ctypes.byref(pt))
    return pt.x, pt.y


def grab(tag, hwnd):
    from PIL import ImageGrab
    sx, sy = client_to_screen(hwnd, *BTN_CLIENT)
    img = ImageGrab.grab(bbox=(sx, sy, sx + BTN_SIZE[0], sy + BTN_SIZE[1]))
    path = OUT + "\\hit_" + tag + ".png"
    img.save(path)
    return img


def diff(a, b):
    pa, pb = a.load(), b.load()
    w, h = a.size
    n = 0
    for y in range(h):
        for x in range(w):
            if pa[x, y] != pb[x, y]:
                n += 1
    return n


proc = subprocess.Popen([CLIENT], cwd=CWD)
hwnd = None
for _ in range(60):
    time.sleep(0.5)
    hwnd = hwnd_of_pid(proc.pid)
    if hwnd:
        break
print("hwnd", hwnd)
time.sleep(12)
print("focused:", force_fg(hwnd))
time.sleep(1)

# 基线：窗口内远离按钮处
bx, by = client_to_screen(hwnd, 60, 60)
user32.SetCursorPos(bx, by)
time.sleep(0.4)
base = grab("0base", hwnd)

tests = [
    ("P1_视觉内旧框外", (395, 375)),
    ("P2_旧框内视觉外", (294, 350)),
    ("CENTER", (347, 365)),
]

for name, (cx, cy) in tests:
    sx, sy = client_to_screen(hwnd, cx, cy)
    user32.SetCursorPos(sx, sy)
    time.sleep(0.45)
    img = grab(name, hwnd)
    print("%-18s client(%d,%d)  与基线差异像素=%d  (光标本身会带 ~80-150)" % (name, cx, cy, diff(base, img)))

print("截图已存 " + OUT)
time.sleep(0.5)
proc.terminate()

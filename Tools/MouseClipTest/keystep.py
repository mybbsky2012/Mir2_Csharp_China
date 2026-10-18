import ctypes, subprocess, time, sys
from ctypes import wintypes
user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

CLIENT = r"E:/micromir2/Client.exe"
CWD = r"E:/micromir2"
SHOTS = r"D:/mir2-20241027/Tools/MouseClipTest"

def hwnd_of_pid(pid):
    found = []
    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD(); user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h): found.append(h)
        return True
    user32.EnumWindows(cb, 0)
    return found[0] if found else None

PUL = ctypes.POINTER(ctypes.c_ulong)
class K(ctypes.Structure):
    _fields_=[("wVk",wintypes.WORD),("wScan",wintypes.WORD),("dwFlags",wintypes.DWORD),("time",wintypes.DWORD),("dwExtraInfo",PUL)]
class M(ctypes.Structure):
    _fields_=[("dx",wintypes.LONG),("dy",wintypes.LONG),("mouseData",wintypes.DWORD),("dwFlags",wintypes.DWORD),("time",wintypes.DWORD),("dwExtraInfo",PUL)]
class H(ctypes.Structure):
    _fields_=[("uMsg",wintypes.DWORD),("wParamL",wintypes.SHORT),("wParamH",wintypes.USHORT)]
class U(ctypes.Union):
    _fields_=[("ki",K),("mi",M),("hi",H)]
class I(ctypes.Structure):
    _fields_=[("type",wintypes.DWORD),("ii",U)]

def key(vk, scan=0, flags=0):
    extra = ctypes.c_ulong(0)
    ii = U(); ii.ki = K(vk, scan, flags, 0, ctypes.pointer(extra))
    x = I(1, ii)
    return user32.SendInput(1, ctypes.pointer(x), ctypes.sizeof(x))

def click(x, y):
    user32.SetCursorPos(x, y)
    time.sleep(0.1)
    user32.mouse_event(2,0,0,0,0); user32.mouse_event(4,0,0,0,0)

proc = subprocess.Popen([CLIENT], cwd=CWD)
hwnd = None
for _ in range(60):
    time.sleep(0.5)
    hwnd = hwnd_of_pid(proc.pid)
    if hwnd: break
print("hwnd", hwnd)
time.sleep(12)
r = wintypes.RECT(); user32.GetWindowRect(hwnd, ctypes.byref(r))
print("winrect", (r.left, r.top, r.right, r.bottom))
user32.SetForegroundWindow(hwnd); time.sleep(1)
print("fg==client:", user32.GetForegroundWindow()==hwnd)

# 点账号输入框（窗口内相对位置 ~ (40%, 47%)）
cx, cy = (r.left+r.right)//2, (r.top+r.bottom)//2
click(cx-60, cy-14); time.sleep(0.5)

# 发字符 'a' 'b' 两种方式：VK 方式
n1 = key(0x41); time.sleep(0.03); key(0x41, up=True)   # 'a'
time.sleep(0.3)
n2 = key(0x42, 0x30, 0); time.sleep(0.03); key(0x42, 0x30, 2)  # 'b' scancode
time.sleep(1)

from PIL import ImageGrab
ImageGrab.grab().save(SHOTS + "\\3_keytest.png")
print("sent:", n1, n2)

# Alt+Enter 组合（VK + 扫描码混合）
key(0x12); time.sleep(0.15)
n3 = key(0x0D, 0x1C); time.sleep(0.05); key(0x0D, 0x1C, 2)
key(0x12, up=True); time.sleep(2.5)
user32.GetWindowRect(hwnd, ctypes.byref(r))
print("after alt+enter winrect", (r.left, r.top, r.right, r.bottom))
style = user32.GetWindowLongW(hwnd, -16) & 0xFFFFFFFF
print("style", hex(style), "caption:", bool(style & 0x00C00000))
ImageGrab.grab().save(SHOTS + "\\4_altenter.png")
proc.terminate()

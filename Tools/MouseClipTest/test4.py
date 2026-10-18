import ctypes, subprocess, time, sys
from ctypes import wintypes
user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32
user32.SetProcessDPIAware()

CLIENT = r"E:/micromir2/Client.exe"
CWD = r"E:/micromir2"
SHOTS = r"D:/mir2-20241027/Tools/MouseClipTest"

def clip_rect():
    r = wintypes.RECT(); user32.GetClipCursor(ctypes.byref(r))
    return (r.left, r.top, r.right, r.bottom)

def screen_rect():
    return (0, 0, user32.GetSystemMetrics(0), user32.GetSystemMetrics(1))

def hwnd_of_pid(pid):
    found = []
    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(h, l):
        p = wintypes.DWORD(); user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h): found.append(h)
        return True
    user32.EnumWindows(cb, 0)
    return found[0] if found else None

def state(hwnd):
    wr = wintypes.RECT(); cr = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(wr))
    user32.GetClientRect(hwnd, ctypes.byref(cr))
    style = user32.GetWindowLongW(hwnd, -16) & 0xFFFFFFFF
    return ("win=%s client=%dx%d caption=%s clip=%s" % ((wr.left,wr.top,wr.right,wr.bottom),
            cr.right-cr.left, cr.bottom-cr.top, bool(style & 0x00C00000), clip_rect()))

def force_foreground(hwnd):
    tid_self = kernel32.GetCurrentThreadId()
    tid_target = user32.GetWindowThreadProcessId(hwnd, None)
    fg = user32.GetForegroundWindow()
    tid_fg = user32.GetWindowThreadProcessId(fg, None)
    user32.AttachThreadInput(tid_self, tid_fg, True)
    user32.AttachThreadInput(tid_self, tid_target, True)
    user32.BringWindowToTop(hwnd); user32.SetForegroundWindow(hwnd); user32.SetFocus(hwnd)
    user32.AttachThreadInput(tid_self, tid_fg, False)
    user32.AttachThreadInput(tid_self, tid_target, False)
    time.sleep(0.5)
    return user32.GetForegroundWindow() == hwnd

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

def key(vk, flags=0):
    extra = ctypes.c_ulong(0)
    ii = U(); ii.ki = K(vk, 0, flags, 0, ctypes.pointer(extra))
    return user32.SendInput(1, ctypes.pointer(I(1, ii)), ctypes.sizeof(I))

def alt_enter():
    key(0x12); time.sleep(0.4)      # ALT 按住久一点
    key(0x0D); time.sleep(0.12)     # ENTER down
    key(0x0D, 2); time.sleep(0.12)  # ENTER up
    key(0x12, 2); time.sleep(0.2)   # ALT up

proc = subprocess.Popen([CLIENT], cwd=CWD)
hwnd = None
for _ in range(60):
    time.sleep(0.5)
    hwnd = hwnd_of_pid(proc.pid)
    if hwnd: break
print("hwnd", hwnd)
time.sleep(12)
print("focused:", force_foreground(hwnd))
time.sleep(1)

wr = wintypes.RECT(); user32.GetWindowRect(hwnd, ctypes.byref(wr))
# 点窗口上部空白（避开按钮/输入框），让焦点脱离 IME
user32.SetCursorPos(wr.left + 120, wr.top + 120)
time.sleep(0.2); user32.mouse_event(2,0,0,0,0); user32.mouse_event(4,0,0,0,0)
time.sleep(0.5)
key(0x1B); time.sleep(0.05); key(0x1B, 2)   # ESC 取消 IME 候选
time.sleep(0.5)

print("A 初始     :", state(hwnd))
alt_enter(); time.sleep(3.5)
print("B 全屏后   :", state(hwnd))
alt_enter(); time.sleep(3.5)
print("C 窗口化后 :", state(hwnd))
final = clip_rect()
print()
print("RESULT:", "PASS —— 窗口化后裁剪已解除" if final == screen_rect() else "FAIL —— 仍被裁剪在 %s" % (final,))

from PIL import ImageGrab
ImageGrab.grab().save(SHOTS + "\\8_final.png")
time.sleep(0.5)
proc.terminate()

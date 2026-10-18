"""压测 VisiblePixel 路径：反复切换全屏（强制纹理重建）+ 鼠标扫控件。"""
import ctypes, subprocess, time, sys
from ctypes import wintypes
user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32
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

def force_fg(hwnd):
    tid_self = kernel32.GetCurrentThreadId()
    tid_t = user32.GetWindowThreadProcessId(hwnd, None)
    fg = user32.GetForegroundWindow()
    tid_fg = user32.GetWindowThreadProcessId(fg, None)
    user32.AttachThreadInput(tid_self, tid_fg, True)
    user32.AttachThreadInput(tid_self, tid_t, True)
    user32.BringWindowToTop(hwnd); user32.SetForegroundWindow(hwnd); user32.SetFocus(hwnd)
    user32.AttachThreadInput(tid_self, tid_fg, False)
    user32.AttachThreadInput(tid_self, tid_t, False)
    time.sleep(0.4)
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
    key(0x12); time.sleep(0.35)
    key(0x0D); time.sleep(0.12)
    key(0x0D, 2); time.sleep(0.12)
    key(0x12, 2); time.sleep(0.2)

def sweep(hwnd):
    """鼠标在窗口内扫一遍，逼 VisiblePixel 反复跑"""
    wr = wintypes.RECT(); user32.GetWindowRect(hwnd, ctypes.byref(wr))
    for gy in range(1, 7):
        for gx in range(1, 13):
            x = wr.left + (wr.right - wr.left) * gx // 13
            y = wr.top + (wr.bottom - wr.top) * gy // 7
            user32.SetCursorPos(x, y)
            time.sleep(0.012)

proc = subprocess.Popen([CLIENT], cwd=CWD)
hwnd = None
for _ in range(60):
    time.sleep(0.5)
    hwnd = hwnd_of_pid(proc.pid)
    if hwnd: break
print("hwnd", hwnd, "pid", proc.pid)
time.sleep(12)
print("focused:", force_fg(hwnd))
time.sleep(1)

from PIL import ImageGrab
ImageGrab.grab().save(SHOTS + "\\A_before_stress.png")

rounds = 8
for i in range(rounds):
    if proc.poll() is not None:
        print("!! 第 %d 轮前进程已退出，exit=%s" % (i, proc.returncode))
        break
    sweep(hwnd)
    alt_enter(); time.sleep(1.8)
    sweep(hwnd)
    alt_enter(); time.sleep(1.8)
    alive = proc.poll() is None
    print("round %2d/%d  alive=%s" % (i + 1, rounds, alive))
    if not alive:
        break

time.sleep(1)
print("final alive:", proc.poll() is None)
ImageGrab.grab().save(SHOTS + "\\B_after_stress.png")
if proc.poll() is None:
    proc.terminate()

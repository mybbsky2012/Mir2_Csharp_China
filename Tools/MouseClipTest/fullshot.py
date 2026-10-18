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

proc = subprocess.Popen([CLIENT], cwd=CWD)
hwnd = None
for _ in range(60):
    time.sleep(0.5)
    hwnd = hwnd_of_pid(proc.pid)
    if hwnd: break
time.sleep(15)
force_fg(hwnd); time.sleep(1)
wr = wintypes.RECT(); cr = wintypes.RECT()
user32.GetWindowRect(hwnd, ctypes.byref(wr)); user32.GetClientRect(hwnd, ctypes.byref(cr))
pt = wintypes.POINT(0,0); user32.ClientToScreen(hwnd, ctypes.byref(pt))
print("win", (wr.left,wr.top,wr.right,wr.bottom), "client", (cr.right,cr.bottom), "clientOrigin", (pt.x,pt.y))
from PIL import ImageGrab
ImageGrab.grab().save(r"D:/mir2-20241027/Tools/MouseClipTest/C_login_full.png")
proc.terminate()

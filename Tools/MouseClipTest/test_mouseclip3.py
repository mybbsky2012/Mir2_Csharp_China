"""v3：强制抢前台 + 真实按键，验证「全屏 -> 窗口化」后鼠标裁剪是否解除。
Windows 前台锁定会让 SetForegroundWindow 静默失败，这里用 AttachThreadInput 绕过。
"""
import ctypes
import subprocess
import sys
import time
from ctypes import wintypes

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32
user32.SetProcessDPIAware()

CLIENT = r"E:\micromir2\Client.exe"
CWD = r"E:\micromir2"
SHOTS = r"D:\mir2-20241027\Tools\MouseClipTest"
GWL_STYLE = -16
WS_CAPTION = 0x00C00000
WM_KEYUP = 0x0101
WM_SYSKEYUP = 0x0105


def clip_rect():
    r = wintypes.RECT()
    user32.GetClipCursor(ctypes.byref(r))
    return (r.left, r.top, r.right, r.bottom)


def screen_rect():
    return (0, 0, user32.GetSystemMetrics(0), user32.GetSystemMetrics(1))


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


def state(hwnd):
    wr = wintypes.RECT()
    cr = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(wr))
    user32.GetClientRect(hwnd, ctypes.byref(cr))
    style = user32.GetWindowLongW(hwnd, GWL_STYLE) & 0xFFFFFFFF
    return ("win=%s client=%dx%d caption=%s clip=%s"
            % ((wr.left, wr.top, wr.right, wr.bottom),
               cr.right - cr.left, cr.bottom - cr.top,
               bool(style & WS_CAPTION), clip_rect()))


def force_foreground(hwnd):
    tid_self = kernel32.GetCurrentThreadId()
    tid_target = user32.GetWindowThreadProcessId(hwnd, None)
    fg = user32.GetForegroundWindow()
    tid_fg = user32.GetWindowThreadProcessId(fg, None)
    user32.AttachThreadInput(tid_self, tid_fg, True)
    user32.AttachThreadInput(tid_self, tid_target, True)
    user32.BringWindowToTop(hwnd)
    user32.SetForegroundWindow(hwnd)
    user32.SetFocus(hwnd)
    user32.AttachThreadInput(tid_self, tid_fg, False)
    user32.AttachThreadInput(tid_self, tid_target, False)
    time.sleep(0.5)
    return user32.GetForegroundWindow() == hwnd


def shot(tag):
    from PIL import ImageGrab
    path = SHOTS + "\\" + tag + ".png"
    ImageGrab.grab().save(path)
    return path


PUL = ctypes.POINTER(ctypes.c_ulong)


class K(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD),
                ("dwExtraInfo", PUL)]


class M(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG),
                ("mouseData", wintypes.DWORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", PUL)]


class H(ctypes.Structure):
    _fields_ = [("uMsg", wintypes.DWORD), ("wParamL", wintypes.SHORT),
                ("wParamH", wintypes.USHORT)]


class U(ctypes.Union):
    _fields_ = [("ki", K), ("mi", M), ("hi", H)]


class I(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("ii", U)]


def key(vk, flags=0):
    extra = ctypes.c_ulong(0)
    ii = U()
    ii.ki = K(vk, 0, flags, 0, ctypes.pointer(extra))
    return user32.SendInput(1, ctypes.pointer(I(1, ii)), ctypes.sizeof(I))


def main():
    prev_fg = user32.GetForegroundWindow()
    print("screen:", screen_rect(), "prev_fg:", prev_fg)

    proc = subprocess.Popen([CLIENT], cwd=CWD)
    hwnd = None
    for _ in range(60):
        time.sleep(0.5)
        hwnd = hwnd_of_pid(proc.pid)
        if hwnd:
            break
    if not hwnd:
        print("!! no client window")
        proc.terminate()
        return 2

    time.sleep(12)
    print("focused:", force_foreground(hwnd))
    time.sleep(1)
    print("A 初始     :", state(hwnd))

    # 先验证键盘链路：点账号框打一个 a
    wr = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(wr))
    user32.SetCursorPos((wr.left + wr.right) // 2 - 60, (wr.top + wr.bottom) // 2 - 14)
    time.sleep(0.2)
    user32.mouse_event(2, 0, 0, 0, 0)
    user32.mouse_event(4, 0, 0, 0, 0)
    time.sleep(0.4)
    key(0x41)
    time.sleep(0.03)
    key(0x41, 2)
    time.sleep(1.2)
    print("打字测试截图:", shot("5_typed"))

    # Alt+Enter -> 全屏
    key(0x12)
    time.sleep(0.12)
    key(0x0D)
    time.sleep(0.05)
    key(0x0D, 2)
    key(0x12, 2)
    time.sleep(3.5)
    print("B 全屏后   :", state(hwnd), shot("6_fullscreen"))

    # Alt+Enter -> 窗口化
    key(0x12)
    time.sleep(0.12)
    key(0x0D)
    time.sleep(0.05)
    key(0x0D, 2)
    key(0x12, 2)
    time.sleep(3.5)
    st = state(hwnd)
    print("C 窗口化后 :", st, shot("7_windowed"))

    final_clip = clip_rect()
    print()
    if final_clip == screen_rect():
        print("RESULT: PASS —— 窗口化后光标可覆盖整屏，裁剪已解除")
    else:
        print("RESULT: FAIL —— 仍被裁剪在", final_clip)

    time.sleep(0.5)
    proc.terminate()
    try:
        if prev_fg:
            user32.SetForegroundWindow(prev_fg)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())

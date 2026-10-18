"""验证「全屏 -> 窗口化」后鼠标裁剪是否正确解除（v2）。
每阶段打印窗口矩形/样式 + clip 矩形，截图存档。
"""
import ctypes
import subprocess
import sys
import time
from ctypes import wintypes

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

CLIENT = r"E:\micromir2\Client.exe"
CWD = r"E:\micromir2"
SHOTS = r"D:\mir2-20241027\Tools\MouseClipTest"
GWL_STYLE = -16
WS_CAPTION = 0x00C00000


def clip_rect():
    r = wintypes.RECT()
    user32.GetClipCursor(ctypes.byref(r))
    return (r.left, r.top, r.right, r.bottom)


def screen_rect():
    return (0, 0, user32.GetSystemMetrics(0), user32.GetSystemMetrics(1))


def pid_of(exe):
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq " + exe, "/FO", "CSV", "/NH"],
                         capture_output=True, text=True).stdout
    for line in out.splitlines():
        parts = [p.strip('"') for p in line.split('","')]
        if len(parts) >= 2 and parts[0].lower() == exe.lower():
            return int(parts[1])
    return None


def hwnd_of_pid(pid):
    found = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def cb(hwnd, lparam):
        p = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(hwnd):
            found.append(hwnd)
        return True

    user32.EnumWindows(cb, 0)
    return found[0] if found else None


def win_state(hwnd):
    wr = wintypes.RECT()
    cr = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(wr))
    user32.GetClientRect(hwnd, ctypes.byref(cr))
    style = user32.GetWindowLongW(hwnd, GWL_STYLE) & 0xFFFFFFFF
    has_caption = bool(style & WS_CAPTION)
    return dict(win=(wr.left, wr.top, wr.right, wr.bottom),
                client=(cr.right - cr.left, cr.bottom - cr.top),
                caption=has_caption)


def shot(tag):
    try:
        from PIL import ImageGrab
        img = ImageGrab.grab()
        path = SHOTS + "\\" + tag + ".png"
        img.save(path)
        return path
    except Exception as e:
        return "screenshot failed: %r" % (e,)


def focus(hwnd, timeout=10):
    end = time.time() + timeout
    while time.time() < end:
        if user32.GetForegroundWindow() == hwnd:
            return True
        user32.SetForegroundWindow(hwnd)
        user32.BringWindowToTop(hwnd)
        time.sleep(0.4)
    return user32.GetForegroundWindow() == hwnd


PUL = ctypes.POINTER(ctypes.c_ulong)


class KeyBdInput(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD),
                ("dwExtraInfo", PUL)]


class HardwareInput(ctypes.Structure):
    _fields_ = [("uMsg", wintypes.DWORD), ("wParamL", wintypes.SHORT),
                ("wParamH", wintypes.USHORT)]


class MouseInput(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG),
                ("mouseData", wintypes.DWORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", PUL)]


class Input_I(ctypes.Union):
    _fields_ = [("ki", KeyBdInput), ("mi", MouseInput), ("hi", HardwareInput)]


class Input(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("ii", Input_I)]


def send_key(vk, up=False):
    extra = ctypes.c_ulong(0)
    ii = Input_I()
    ii.ki = KeyBdInput(vk, 0, 2 if up else 0, 0, ctypes.pointer(extra))
    x = Input(ctypes.c_ulong(1), ii)
    user32.SendInput(1, ctypes.pointer(x), ctypes.sizeof(x))


def alt_enter():
    send_key(0x12)              # ALT down
    time.sleep(0.05)
    send_key(0x0D)              # ENTER down
    time.sleep(0.05)
    send_key(0x0D, up=True)     # ENTER up
    send_key(0x12, up=True)     # ALT up


def main():
    print("screen      :", screen_rect())
    print("clip(before):", clip_rect())

    proc = subprocess.Popen([CLIENT], cwd=CWD)

    hwnd = None
    for _ in range(60):
        time.sleep(0.5)
        hwnd = hwnd_of_pid(proc.pid)
        if hwnd:
            break
    if not hwnd:
        print("!! 找不到客户端窗口")
        proc.terminate()
        return 2
    print("hwnd        :", hwnd, "pid", proc.pid)

    time.sleep(12)  # 等资源/皮肤加载完
    ok = focus(hwnd)
    time.sleep(1.5)
    print("focused     :", ok)
    st = win_state(hwnd)
    print("A 初始      : %s clip=%s shot=%s" % (st, clip_rect(), shot("0_initial")))

    alt_enter()
    time.sleep(4)
    st = win_state(hwnd)
    print("B 全屏后    : %s clip=%s shot=%s" % (st, clip_rect(), shot("1_fullscreen")))

    alt_enter()
    time.sleep(4)
    ok2 = focus(hwnd)
    time.sleep(1)
    st = win_state(hwnd)
    final = clip_rect()
    print("C 窗口化后  : %s clip=%s focused=%s shot=%s" % (st, final, ok2, shot("2_windowed")))

    print()
    if final == screen_rect():
        print("RESULT: PASS —— 窗口化后裁剪已解除（整屏可用）")
    else:
        print("RESULT: FAIL —— 窗口化后仍被裁剪在 %s" % (final,))

    time.sleep(1)
    proc.terminate()
    return 0


if __name__ == "__main__":
    sys.exit(main())

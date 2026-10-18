import ctypes, time
from ctypes import wintypes
user32 = ctypes.windll.user32
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
print("sizeof(INPUT) =", ctypes.sizeof(I))
extra = ctypes.c_ulong(0)
ii = U(); ii.ki = K(0x2D, 0, 0, 0, ctypes.pointer(extra))   # INSERT
x = I(1, ii)
user32.SendInput.restype = wintypes.UINT
n = user32.SendInput(1, ctypes.pointer(x), ctypes.sizeof(x))
print("SendInput ->", n, "lasterr", ctypes.GetLastError())
print("foreground hwnd", user32.GetForegroundWindow())
gw = user32.GetForegroundWindow()
buf = ctypes.create_unicode_buffer(256)
user32.GetWindowTextW(gw, buf, 256)
print("foreground title:", repr(buf.value))

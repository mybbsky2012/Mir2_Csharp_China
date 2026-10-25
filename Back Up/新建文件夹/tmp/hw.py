# -*- coding: utf-8 -*-
"""采集本机硬件与进程资源，用于评估微端服务负载能力。"""
import ctypes, os, subprocess, sys, time

class MEMORYSTATUSEX(ctypes.Structure):
    _fields_ = [("dwLength", ctypes.c_ulong),
                ("dwMemoryLoad", ctypes.c_ulong),
                ("ullTotalPhys", ctypes.c_ulonglong),
                ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong),
                ("ullAvailPageFile", ctypes.c_ulonglong),
                ("ullTotalVirtual", ctypes.c_ulonglong),
                ("ullAvailVirtual", ctypes.c_ulonglong),
                ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]

def mem():
    m = MEMORYSTATUSEX()
    m.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    return m

m = mem()
print("CPU 逻辑核心 : %s" % os.cpu_count())
print("总内存       : %.1f GB" % (m.ullTotalPhys / 1024 ** 3))
print("可用内存     : %.1f GB" % (m.ullAvailPhys / 1024 ** 3))
print("内存负载     : %d%%" % m.dwMemoryLoad)

try:
    import psutil
    print("psutil       : 可用 %s" % psutil.__version__)
    vm = psutil.virtual_memory()
    print("psutil 内存  : 总 %.1f GB 可用 %.1f GB %d%%" % (vm.total/1024**3, vm.available/1024**3, vm.percent))
    for d in psutil.disk_partitions():
        try:
            u = psutil.disk_usage(d.mountpoint)
            print("  分区 %-4s 总 %.0fGB 已用 %.0fGB %d%%" % (d.mountpoint, u.total/1024**3, u.used/1024**3, u.percent))
        except Exception:
            pass
except ImportError:
    print("psutil       : 未安装")

# 目标进程
pid = int(sys.argv[1]) if len(sys.argv) > 1 else 3188
try:
    import psutil
    p = psutil.Process(pid)
    print("--- 进程 %d (%s) ---" % (pid, p.name()))
    print("  内存 %.1f MB   线程 %d" % (p.memory_info().rss/1024**2, p.num_threads()))
except Exception as e:
    print("进程 %d 读取失败: %s" % (pid, e))

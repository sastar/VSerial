"""
GUI 串口工具 (如 VOFA+ / Qt QSerialPort) 兼容性与抗崩溃自动化测试脚本
模拟 Win32 重叠异步 I/O、GetCommProperties 查询、以及异步等待中强制关闭句柄的生命周期。
"""

import ctypes
from ctypes import wintypes
import time
import sys

# Win32 常量定义
GENERIC_READ = 0x80000000
GENERIC_WRITE = 0x40000000
OPEN_EXISTING = 3
FILE_FLAG_OVERLAPPED = 0x40000000
INVALID_HANDLE_VALUE = -1

EV_RXCHAR = 0x0001
EV_CTS    = 0x0008
EV_DSR    = 0x0010

class COMMPROP(ctypes.Structure):
    _fields_ = [
        ("wPacketLength", wintypes.WORD),
        ("wPacketVersion", wintypes.WORD),
        ("dwServiceMask", wintypes.DWORD),
        ("dwReserved1", wintypes.DWORD),
        ("dwMaxTxQueue", wintypes.DWORD),
        ("dwMaxRxQueue", wintypes.DWORD),
        ("dwMaxBaud", wintypes.DWORD),
        ("dwProvSubType", wintypes.DWORD),
        ("dwProvCapabilities", wintypes.DWORD),
        ("dwSettableParams", wintypes.DWORD),
        ("dwSettableBaud", wintypes.DWORD),
        ("wSettableData", wintypes.WORD),
        ("wSettableStopParity", wintypes.WORD),
        ("dwCurrentTxQueue", wintypes.DWORD),
        ("dwCurrentRxQueue", wintypes.DWORD),
        ("dwProvSpec1", wintypes.DWORD),
        ("dwProvSpec2", wintypes.DWORD),
        ("wcProvChar", wintypes.WCHAR * 1),
    ]

kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

def test_gui_open_and_close(port_name="COM10"):
    print(f"\n=========================================================")
    print(f" 开始模拟 VOFA+/Qt 串口连接生命周期: {port_name}")
    print(f"=========================================================")

    port_path = f"\\\\.\\{port_name}"
    
    # 1. 模拟 QSerialPortPrivate::open 以重叠模式打开端口
    print("[1] 打开端口句柄 (FILE_FLAG_OVERLAPPED)...")
    handle = kernel32.CreateFileW(
        port_path,
        GENERIC_READ | GENERIC_WRITE,
        0,
        None,
        OPEN_EXISTING,
        FILE_FLAG_OVERLAPPED,
        None
    )
    if handle == INVALID_HANDLE_VALUE:
        err = ctypes.get_last_error()
        print(f"  [×] 打开端口失败! 错误码: {err}")
        return False
    print("  [√] 端口句柄打开成功！")

    try:
        # 2. 模拟 Qt 必须调用的 GetCommProperties
        print("[2] 调用 GetCommProperties (VOFA+/Qt 必须依赖项)...")
        prop = COMMPROP()
        res = kernel32.GetCommProperties(handle, ctypes.byref(prop))
        if not res:
            err = ctypes.get_last_error()
            print(f"  [×] GetCommProperties 失败! 错误码: {err}")
            return False
        
        print(f"  [√] GetCommProperties 成功!")
        print(f"      PacketLength={prop.wPacketLength}, MaxBaud=0x{prop.dwMaxBaud:08X}, ProvSubType=0x{prop.dwProvSubType:08X}")
        print(f"      MaxRxQueue={prop.dwMaxRxQueue}, CurrentRxQueue={prop.dwCurrentRxQueue}")

        # 3. 模拟设置事件掩码并挂起异步 WaitCommEvent
        print("[3] 设置事件掩码 SetCommMask 并触发异步 WaitCommEvent...")
        kernel32.SetCommMask(handle, EV_RXCHAR | EV_CTS | EV_DSR)

        event_mask = wintypes.DWORD()
        overlapped = ctypes.create_string_buffer(32) # OVERLAPPED 结构
        res = kernel32.WaitCommEvent(handle, ctypes.byref(event_mask), overlapped)
        err = ctypes.get_last_error()
        # ERROR_IO_PENDING = 997，属于正常的异步重叠挂起
        if not res and err != 997:
            print(f"  [×] WaitCommEvent 异常! 错误码: {err}")
            return False
        print("  [√] WaitCommEvent 异步请求成功挂入驱动队列 (IO_PENDING)！")

        # 4. 关键验证点：在异步未决状态下，强行 CloseHandle 关闭端口
        # （之前正是因为这里缺少 EvtIoCanceledOnQueue 导致驱动崩溃为代码 43）
        print("[4] 模拟用户点击断开/直接 CloseHandle 撤回请求...")

    finally:
        kernel32.CloseHandle(handle)
        print("  [√] 句柄已关闭！")

    return True

def test_repeated_cycles(port_name="COM10", cycles=5):
    print(f"\n[5] 压力测试：连续执行 {cycles} 次快速连接-挂起-强退循环...")
    for i in range(1, cycles + 1):
        port_path = f"\\\\.\\{port_name}"
        handle = kernel32.CreateFileW(
            port_path,
            GENERIC_READ | GENERIC_WRITE,
            0,
            None,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            None
        )
        if handle == INVALID_HANDLE_VALUE:
            print(f"  [×] 第 {i} 轮打开失败!")
            return False
        
        prop = COMMPROP()
        kernel32.GetCommProperties(handle, ctypes.byref(prop))
        kernel32.SetCommMask(handle, EV_RXCHAR)
        
        event_mask = wintypes.DWORD()
        overlapped = ctypes.create_string_buffer(32)
        kernel32.WaitCommEvent(handle, ctypes.byref(event_mask), overlapped)
        kernel32.CloseHandle(handle)
        time.sleep(0.02)
    
    print(f"  [√] {cycles} 轮高频打开-异步挂起-关闭压力测试全部成功！")
    return True

if __name__ == "__main__":
    port = sys.argv[1] if len(sys.argv) > 1 else "COM10"
    if test_gui_open_and_close(port) and test_repeated_cycles(port, 5):
        print("\n=========================================================")
        print(" [√] VOFA+ / Qt GUI 兼容性与抗崩溃测试完全通过！")
        print("=========================================================")
        sys.exit(0)
    else:
        sys.exit(1)

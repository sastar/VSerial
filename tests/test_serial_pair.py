"""
VSerial 成对虚拟串口自动化互通与流控测试脚本
用法:
    python test_serial_pair.py --portA COM10 --portB COM11
"""

import argparse
import hashlib
import os
import sys
import time

try:
    import serial
except ImportError:
    print("[!] 错误: 未安装 pyserial 模块，请先运行: pip install pyserial")
    sys.exit(1)


def test_text_echo(serA, serB):
    print("\n[测试 1] 文本数据双向收发测试...")
    test_msg_A = b"Hello from Port A! 123456\r\n"
    test_msg_B = b"Hello from Port B! ABCDEF\r\n"

    # A -> B
    serA.write(test_msg_A)
    time.sleep(0.05)
    recv_B = serB.read(serB.in_waiting or len(test_msg_A))
    assert recv_B == test_msg_A, f"A -> B 接收不一致! 期望: {test_msg_A}, 实际: {recv_B}"
    print("  [√] A -> B 文本发送与接收校验成功！")

    # B -> A
    serB.write(test_msg_B)
    time.sleep(0.05)
    recv_A = serA.read(serA.in_waiting or len(test_msg_B))
    assert recv_A == test_msg_B, f"B -> A 接收不一致! 期望: {test_msg_B}, 实际: {recv_A}"
    print("  [√] B -> A 文本发送与接收校验成功！")


def test_burst_buffer(serA, serB, size_kb=32):
    print(f"\n[测试 2] 驱动内置缓冲区突发暂存测试 ({size_kb} KB)...")
    payload = os.urandom(size_kb * 1024)
    expected_md5 = hashlib.md5(payload).hexdigest()

    serA.reset_input_buffer()
    serB.reset_input_buffer()

    start_time = time.time()
    serA.write(payload)

    # 循环读取直到取满
    received = bytearray()
    timeout = 3.0
    recv_start = time.time()
    while len(received) < len(payload) and (time.time() - recv_start < timeout):
        available = serB.in_waiting
        if available > 0:
            received.extend(serB.read(available))
        else:
            time.sleep(0.005)

    duration = time.time() - start_time
    actual_md5 = hashlib.md5(received).hexdigest()

    assert len(received) == len(payload), f"数据长度不符! 发送: {len(payload)}, 接收: {len(received)}"
    assert actual_md5 == expected_md5, f"MD5 校验失败! 发送: {expected_md5}, 接收: {actual_md5}"

    speed_mb = (len(payload) / (1024 * 1024)) / duration
    print(f"  [√] {size_kb} KB 突发暂存传输成功！MD5 一致: {actual_md5}")
    print(f"      传输耗时: {duration * 1000:.2f} ms，传输速率: {speed_mb:.2f} MB/s")


def test_streaming_transfer(serA, serB, size_mb=1):
    import threading

    total_bytes = size_mb * 1024 * 1024
    chunk_size = 4096

    # 1. A -> B 传输
    print(f"\n[测试 3.1] A -> B 单向流式传输 ({size_mb} MB)...")
    payload_A = os.urandom(total_bytes)
    md5_A = hashlib.md5(payload_A).hexdigest()
    serA.reset_input_buffer()
    serB.reset_input_buffer()

    recvd_B = bytearray()
    def recv_worker_B():
        while len(recvd_B) < total_bytes:
            chunk = serB.read(min(8192, total_bytes - len(recvd_B)))
            if chunk:
                recvd_B.extend(chunk)
            else:
                break

    t_b = threading.Thread(target=recv_worker_B)
    t_b.start()
    t0 = time.time()
    for i in range(0, total_bytes, chunk_size):
        serA.write(payload_A[i:i + chunk_size])
        time.sleep(0.0002)
    t_b.join(10.0)
    dur_A = time.time() - t0
    assert len(recvd_B) == total_bytes, f"A->B 长度不符! 发送: {total_bytes}, 接收: {len(recvd_B)}"
    assert hashlib.md5(recvd_B).hexdigest() == md5_A, "A->B MD5 校验失败!"
    print(f"  [√] A -> B 传输成功！耗时: {dur_A * 1000:.2f} ms，速率: {(total_bytes / (1024 * 1024)) / dur_A:.2f} MB/s")

    # 2. B -> A 传输
    print(f"\n[测试 3.2] B -> A 单向流式传输 ({size_mb} MB)...")
    payload_B = os.urandom(total_bytes)
    md5_B = hashlib.md5(payload_B).hexdigest()
    serA.reset_input_buffer()
    serB.reset_input_buffer()

    recvd_A = bytearray()
    def recv_worker_A():
        while len(recvd_A) < total_bytes:
            chunk = serA.read(min(8192, total_bytes - len(recvd_A)))
            if chunk:
                recvd_A.extend(chunk)
            else:
                break

    t_a = threading.Thread(target=recv_worker_A)
    t_a.start()
    t0 = time.time()
    for i in range(0, total_bytes, chunk_size):
        serB.write(payload_B[i:i + chunk_size])
        time.sleep(0.0002)
    t_a.join(10.0)
    dur_B = time.time() - t0
    assert len(recvd_A) == total_bytes, f"B->A 长度不符! 发送: {total_bytes}, 接收: {len(recvd_A)}"
    assert hashlib.md5(recvd_A).hexdigest() == md5_B, "B->A MD5 校验失败!"
    print(f"  [√] B -> A 传输成功！耗时: {dur_B * 1000:.2f} ms，速率: {(total_bytes / (1024 * 1024)) / dur_B:.2f} MB/s")

    # 3. A <--> B 同时双向全双工对飙传输
    print(f"\n[测试 3.3] A <--> B 双向同时全双工高吞吐对飙传输 ({size_mb} MB x 2)...")
    payload_AB = os.urandom(total_bytes)
    payload_BA = os.urandom(total_bytes)
    md5_AB = hashlib.md5(payload_AB).hexdigest()
    md5_BA = hashlib.md5(payload_BA).hexdigest()
    serA.reset_input_buffer()
    serB.reset_input_buffer()

    res_B = bytearray()
    res_A = bytearray()

    def worker_read_B():
        while len(res_B) < total_bytes:
            chunk = serB.read(min(8192, total_bytes - len(res_B)))
            if chunk: res_B.extend(chunk)
            else: break

    def worker_read_A():
        while len(res_A) < total_bytes:
            chunk = serA.read(min(8192, total_bytes - len(res_A)))
            if chunk: res_A.extend(chunk)
            else: break

    def worker_send_A():
        for i in range(0, total_bytes, chunk_size):
            serA.write(payload_AB[i:i + chunk_size])
            time.sleep(0.0002)

    def worker_send_B():
        for i in range(0, total_bytes, chunk_size):
            serB.write(payload_BA[i:i + chunk_size])
            time.sleep(0.0002)

    threads = [
        threading.Thread(target=worker_read_B),
        threading.Thread(target=worker_read_A),
        threading.Thread(target=worker_send_A),
        threading.Thread(target=worker_send_B),
    ]

    t0 = time.time()
    for t in threads:
        t.start()
    for t in threads:
        t.join(15.0)
    dur_full = time.time() - t0

    assert len(res_B) == total_bytes, f"全双工 A->B 接收长度不符: {len(res_B)}"
    assert len(res_A) == total_bytes, f"全双工 B->A 接收长度不符: {len(res_A)}"
    assert hashlib.md5(res_B).hexdigest() == md5_AB, "全双工 A->B MD5 错误!"
    assert hashlib.md5(res_A).hexdigest() == md5_BA, "全双工 B->A MD5 错误!"

    total_mb = (total_bytes * 2) / (1024 * 1024)
    print(f"  [√] A <--> B 同时双向全双工传输成功！总传输: {total_mb:.1f} MB")
    print(f"      总耗时: {dur_full * 1000:.2f} ms，双向合计吞吐率: {total_mb / dur_full:.2f} MB/s")



def test_modem_signals(serA, serB):
    print("\n[测试 4] Null-Modem 控制线联锁状态映射测试 (RTS <-> CTS, DTR <-> DSR)...")

    # 1. 测试 RTS -> CTS
    serA.rts = True
    time.sleep(0.05)
    cts_B = serB.cts
    print(f"  A(RTS=True)  ==> B(CTS={cts_B})")

    serA.rts = False
    time.sleep(0.05)
    cts_B = serB.cts
    print(f"  A(RTS=False) ==> B(CTS={cts_B})")

    # 2. 测试 DTR -> DSR
    serA.dtr = True
    time.sleep(0.05)
    dsr_B = serB.dsr
    print(f"  A(DTR=True)  ==> B(DSR={dsr_B})")

    serA.dtr = False
    time.sleep(0.05)
    dsr_B = serB.dsr
    print(f"  A(DTR=False) ==> B(DSR={dsr_B})")

    print("  [√] 控制线状态联锁测试完成！")


def main():
    parser = argparse.ArgumentParser(description="VSerial 虚拟串口对互通测试工具")
    parser.add_argument("--portA", default="COM10", help="端口 A (默认: COM10)")
    parser.add_argument("--portB", default="COM11", help="端口 B (默认: COM11)")
    parser.add_argument("--baud", type=int, default=115200, help="波特率 (默认: 115200)")
    args = parser.parse_args()

    print("=========================================================")
    print(f" 开始测试成对虚拟串口: {args.portA} <==> {args.portB} (波特率: {args.baud})")
    print("=========================================================")

    try:
        serA = serial.Serial(args.portA, args.baud, timeout=1.0)
        serB = serial.Serial(args.portB, args.baud, timeout=1.0)
    except Exception as e:
        print(f"[!] 无法打开串口: {e}")
        print("    请确认已通过命令行创建该串口对，例如:")
        print(f"    vserial pair add --id 1 --portA {args.portA} --portB {args.portB}")
        return 1

    try:
        test_text_echo(serA, serB)
        test_burst_buffer(serA, serB, size_kb=32)
        test_streaming_transfer(serA, serB, size_mb=1)
        test_modem_signals(serA, serB)
        print("\n=========================================================")
        print(" [√] 所有测试用例全部通过！虚拟串口对运行完全正常！")
        print("=========================================================")
    finally:
        serA.close()
        serB.close()

    return 0


if __name__ == "__main__":
    sys.exit(main())

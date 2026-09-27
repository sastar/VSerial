/*++

Copyright (C) VSerial Project, All Rights Reserved.

Module Name:

    pair.h

Abstract:

    This module defines the interfaces and data structures for the
    paired virtual serial port channel manager.

Environment:

    Windows Driver Framework (UMDF 2.0)

--*/

#pragma once

#include <windows.h>
#include "ringbuffer.h"

// 前向声明设备上下文结构体
struct _DEVICE_CONTEXT;

// 双向配对环形缓冲区大小（128KB，确保完整容纳64KB及以上突发数据包）
#define PAIR_BUFFER_SIZE (128 * 1024)

// 配对角色定义
#define PAIR_ROLE_A 0
#define PAIR_ROLE_B 1

// 成对通道结构体
typedef struct _SERIAL_PAIR_CHANNEL {
    ULONG                       PairId;             // 配对标识
    SRWLOCK                     Lock;               // 通道互斥锁

    struct _DEVICE_CONTEXT      *DeviceA;           // A 端设备指针 (Role 0)
    struct _DEVICE_CONTEXT      *DeviceB;           // B 端设备指针 (Role 1)

    // A -> B 数据缓冲区
    RING_BUFFER                 BufferAtoB;
    BYTE                        RawBufferAtoB[PAIR_BUFFER_SIZE];

    // B -> A 数据缓冲区
    RING_BUFFER                 BufferBtoA;
    BYTE                        RawBufferBtoA[PAIR_BUFFER_SIZE];

    // 控制线输出寄存器 (Modem Control)
    ULONG                       ModemControlA;
    ULONG                       ModemControlB;

    struct _SERIAL_PAIR_CHANNEL *Next;              // 全局管理器链表下一节点
} SERIAL_PAIR_CHANNEL, *PSERIAL_PAIR_CHANNEL;

// 全局配对管理器接口
VOID
PairManager_Initialize(
    VOID
    );

VOID
PairManager_Cleanup(
    VOID
    );

NTSTATUS
PairManager_RegisterDevice(
    _In_ struct _DEVICE_CONTEXT *DeviceContext,
    _In_ ULONG                  PairId,
    _In_ ULONG                  PairRole
    );

VOID
PairManager_UnregisterDevice(
    _In_ struct _DEVICE_CONTEXT *DeviceContext
    );

struct _DEVICE_CONTEXT *
PairManager_GetPeerDevice(
    _In_ struct _DEVICE_CONTEXT *DeviceContext
    );

VOID
PairChannel_Lock(
    _In_ PSERIAL_PAIR_CHANNEL   Channel
    );

VOID
PairChannel_Unlock(
    _In_ PSERIAL_PAIR_CHANNEL   Channel
    );

PRING_BUFFER
PairChannel_GetTxBuffer(
    _In_ struct _DEVICE_CONTEXT *DeviceContext
    );

PRING_BUFFER
PairChannel_GetRxBuffer(
    _In_ struct _DEVICE_CONTEXT *DeviceContext
    );

VOID
PairChannel_UpdateModemLines(
    _In_ struct _DEVICE_CONTEXT *SourceDevice,
    _In_ ULONG                  NewModemControl
    );

VOID
PairChannel_NotifyRxChar(
    _In_ struct _DEVICE_CONTEXT *TargetDevice
    );

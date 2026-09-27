/*++

Copyright (C) VSerial Project, All Rights Reserved.

Module Name:

    pair.c

Abstract:

    This module implements the paired virtual serial port channel manager,
    cross-link buffer mapping, and Null-Modem control signal interconnect.

Environment:

    Windows Driver Framework (UMDF 2.0)

--*/

#include "internal.h"

// 全局配对管理器单例结构
typedef struct _PAIR_MANAGER {
    SRWLOCK                 Lock;
    PSERIAL_PAIR_CHANNEL    ChannelListHead;
    BOOLEAN                 Initialized;
} PAIR_MANAGER, *PPAIR_MANAGER;

static PAIR_MANAGER g_PairManager = {
    SRWLOCK_INIT,
    NULL,
    FALSE
};

VOID
PairManager_Initialize(
    VOID
    )
{
    if (!g_PairManager.Initialized) {
        InitializeSRWLock(&g_PairManager.Lock);
        g_PairManager.ChannelListHead = NULL;
        g_PairManager.Initialized = TRUE;
    }
}

VOID
PairManager_Cleanup(
    VOID
    )
{
    PSERIAL_PAIR_CHANNEL channel;
    PSERIAL_PAIR_CHANNEL next;

    AcquireSRWLockExclusive(&g_PairManager.Lock);
    channel = g_PairManager.ChannelListHead;
    while (channel != NULL) {
        next = channel->Next;
        HeapFree(GetProcessHeap(), 0, channel);
        channel = next;
    }
    g_PairManager.ChannelListHead = NULL;
    g_PairManager.Initialized = FALSE;
    ReleaseSRWLockExclusive(&g_PairManager.Lock);
}

NTSTATUS
PairManager_RegisterDevice(
    _In_ PDEVICE_CONTEXT DeviceContext,
    _In_ ULONG           PairId,
    _In_ ULONG           PairRole
    )
{
    PSERIAL_PAIR_CHANNEL channel = NULL;

    PairManager_Initialize();

    AcquireSRWLockExclusive(&g_PairManager.Lock);

    // 查找是否已存在相同 PairId 的通道
    for (channel = g_PairManager.ChannelListHead; channel != NULL; channel = channel->Next) {
        if (channel->PairId == PairId) {
            break;
        }
    }

    // 若不存在，则创建并初始化新通道
    if (channel == NULL) {
        channel = (PSERIAL_PAIR_CHANNEL)HeapAlloc(
            GetProcessHeap(),
            HEAP_ZERO_MEMORY,
            sizeof(SERIAL_PAIR_CHANNEL)
        );

        if (channel == NULL) {
            ReleaseSRWLockExclusive(&g_PairManager.Lock);
            Trace(TRACE_LEVEL_ERROR, "Error: Failed to allocate SERIAL_PAIR_CHANNEL");
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        channel->PairId = PairId;
        InitializeSRWLock(&channel->Lock);

        RingBufferInitialize(
            &channel->BufferAtoB,
            channel->RawBufferAtoB,
            sizeof(channel->RawBufferAtoB)
        );

        RingBufferInitialize(
            &channel->BufferBtoA,
            channel->RawBufferBtoA,
            sizeof(channel->RawBufferBtoA)
        );

        channel->Next = g_PairManager.ChannelListHead;
        g_PairManager.ChannelListHead = channel;
    }

    // 绑定设备角色
    AcquireSRWLockExclusive(&channel->Lock);
    if (PairRole == PAIR_ROLE_A) {
        channel->DeviceA = DeviceContext;
    } else {
        channel->DeviceB = DeviceContext;
    }

    DeviceContext->PairId = PairId;
    DeviceContext->PairRole = PairRole;
    DeviceContext->PairChannel = channel;

    // 默认调制解调器状态初始化：成对连接时双方默认接通（CTS/DSR/DCD）
    DeviceContext->ModemStatus = SERIAL_CTS_STATE | SERIAL_DSR_STATE | SERIAL_DCD_STATE;

    ReleaseSRWLockExclusive(&channel->Lock);
    ReleaseSRWLockExclusive(&g_PairManager.Lock);

    Trace(TRACE_LEVEL_INFO, "PairManager: Registered Device 0x%p to PairId %lu Role %lu",
          DeviceContext, PairId, PairRole);

    return STATUS_SUCCESS;
}

VOID
PairManager_UnregisterDevice(
    _In_ PDEVICE_CONTEXT DeviceContext
    )
{
    PSERIAL_PAIR_CHANNEL channel;

    if (DeviceContext == NULL || DeviceContext->PairChannel == NULL) {
        return;
    }

    channel = DeviceContext->PairChannel;

    AcquireSRWLockExclusive(&g_PairManager.Lock);
    AcquireSRWLockExclusive(&channel->Lock);

    if (DeviceContext->PairRole == PAIR_ROLE_A) {
        channel->DeviceA = NULL;
    } else {
        channel->DeviceB = NULL;
    }
    DeviceContext->PairChannel = NULL;

    ReleaseSRWLockExclusive(&channel->Lock);

    // 若两端均已解绑，则销毁通道
    if (channel->DeviceA == NULL && channel->DeviceB == NULL) {
        PSERIAL_PAIR_CHANNEL curr = g_PairManager.ChannelListHead;
        PSERIAL_PAIR_CHANNEL prev = NULL;

        while (curr != NULL) {
            if (curr == channel) {
                if (prev == NULL) {
                    g_PairManager.ChannelListHead = curr->Next;
                } else {
                    prev->Next = curr->Next;
                }
                break;
            }
            prev = curr;
            curr = curr->Next;
        }

        ReleaseSRWLockExclusive(&g_PairManager.Lock);
        HeapFree(GetProcessHeap(), 0, channel);
        Trace(TRACE_LEVEL_INFO, "PairManager: Channel for PairId %lu destroyed", DeviceContext->PairId);
        return;
    }

    ReleaseSRWLockExclusive(&g_PairManager.Lock);
}

struct _DEVICE_CONTEXT *
PairManager_GetPeerDevice(
    _In_ PDEVICE_CONTEXT DeviceContext
    )
{
    PSERIAL_PAIR_CHANNEL channel;
    struct _DEVICE_CONTEXT *peer = NULL;

    if (DeviceContext == NULL || DeviceContext->PairChannel == NULL) {
        return NULL;
    }

    channel = DeviceContext->PairChannel;
    AcquireSRWLockShared(&channel->Lock);

    if (DeviceContext->PairRole == PAIR_ROLE_A) {
        peer = channel->DeviceB;
    } else {
        peer = channel->DeviceA;
    }

    ReleaseSRWLockShared(&channel->Lock);
    return peer;
}

VOID
PairChannel_Lock(
    _In_ PSERIAL_PAIR_CHANNEL Channel
    )
{
    AcquireSRWLockExclusive(&Channel->Lock);
}

VOID
PairChannel_Unlock(
    _In_ PSERIAL_PAIR_CHANNEL Channel
    )
{
    ReleaseSRWLockExclusive(&Channel->Lock);
}

PRING_BUFFER
PairChannel_GetTxBuffer(
    _In_ PDEVICE_CONTEXT DeviceContext
    )
{
    PSERIAL_PAIR_CHANNEL channel = DeviceContext->PairChannel;
    if (channel == NULL) {
        return NULL;
    }

    if (DeviceContext->PairRole == PAIR_ROLE_A) {
        return &channel->BufferAtoB;
    } else {
        return &channel->BufferBtoA;
    }
}

PRING_BUFFER
PairChannel_GetRxBuffer(
    _In_ PDEVICE_CONTEXT DeviceContext
    )
{
    PSERIAL_PAIR_CHANNEL channel = DeviceContext->PairChannel;
    if (channel == NULL) {
        return NULL;
    }

    if (DeviceContext->PairRole == PAIR_ROLE_A) {
        return &channel->BufferBtoA;
    } else {
        return &channel->BufferAtoB;
    }
}

VOID
PairChannel_UpdateModemLines(
    _In_ PDEVICE_CONTEXT SourceDevice,
    _In_ ULONG           NewModemControl
    )
{
    PSERIAL_PAIR_CHANNEL channel = SourceDevice->PairChannel;
    PDEVICE_CONTEXT peerDevice;
    ULONG eventMask = 0;

    if (channel == NULL) {
        return;
    }

    AcquireSRWLockExclusive(&channel->Lock);

    if (SourceDevice->PairRole == PAIR_ROLE_A) {
        channel->ModemControlA = NewModemControl;
        peerDevice = channel->DeviceB;
    } else {
        channel->ModemControlB = NewModemControl;
        peerDevice = channel->DeviceA;
    }

    if (peerDevice != NULL) {
        // Null-Modem 交叉映射逻辑：
        // 1. 本端 RTS 对应对端 CTS
        if (NewModemControl & SERIAL_MCR_RTS) {
            peerDevice->ModemStatus |= SERIAL_CTS_STATE;
        } else {
            peerDevice->ModemStatus &= ~SERIAL_CTS_STATE;
        }
        eventMask |= SERIAL_EV_CTS;

        // 2. 本端 DTR 对应对端 DSR 与 DCD (RLSD)
        if (NewModemControl & SERIAL_MCR_DTR) {
            peerDevice->ModemStatus |= (SERIAL_DSR_STATE | SERIAL_DCD_STATE);
        } else {
            peerDevice->ModemStatus &= ~(SERIAL_DSR_STATE | SERIAL_DCD_STATE);
        }
        eventMask |= (SERIAL_EV_DSR | SERIAL_EV_RLSD);

        // 唤醒对端正在等待的事件请求
        if (peerDevice->WaitMaskQueue != NULL && (peerDevice->WaitMask & eventMask)) {
            WDFREQUEST pendingRequest;
            NTSTATUS status = WdfIoQueueRetrieveNextRequest(peerDevice->WaitMaskQueue, &pendingRequest);
            if (NT_SUCCESS(status)) {
                ULONG triggeredEvents = peerDevice->WaitMask & eventMask;
                // 唤醒并返回
                RequestCopyFromBuffer(pendingRequest, &triggeredEvents, sizeof(ULONG));
                WdfRequestComplete(pendingRequest, STATUS_SUCCESS);
            } else {
                // 暂存历史事件
                peerDevice->HistoryEvents |= (peerDevice->WaitMask & eventMask);
            }
        }
    }

    ReleaseSRWLockExclusive(&channel->Lock);
}

VOID
PairChannel_NotifyRxChar(
    _In_ PDEVICE_CONTEXT TargetDevice
    )
{
    if (TargetDevice == NULL) {
        return;
    }

    // 若目标设备设置了 SERIAL_EV_RXCHAR 监听
    if (TargetDevice->WaitMaskQueue != NULL && (TargetDevice->WaitMask & SERIAL_EV_RXCHAR)) {
        WDFREQUEST pendingRequest;
        NTSTATUS status = WdfIoQueueRetrieveNextRequest(TargetDevice->WaitMaskQueue, &pendingRequest);
        if (NT_SUCCESS(status)) {
            ULONG triggeredEvents = SERIAL_EV_RXCHAR;
            RequestCopyFromBuffer(pendingRequest, &triggeredEvents, sizeof(ULONG));
            WdfRequestComplete(pendingRequest, STATUS_SUCCESS);
        } else {
            TargetDevice->HistoryEvents |= SERIAL_EV_RXCHAR;
        }
    }
}

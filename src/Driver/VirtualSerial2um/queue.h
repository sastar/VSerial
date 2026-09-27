/*++

Copyright (c) Microsoft Corporation, All Rights Reserved

Module Name:

    queue.h

Abstract:

    This file defines the queue callback interface.

Environment:

    Windows Driver Framework

--*/

#pragma once

#include "internal.h"

// Set ring buffer size
#define DATA_BUFFER_SIZE 1024

//
// Device states
//
#define COMMAND_MATCH_STATE_IDLE   0
#define COMMAND_MATCH_STATE_GOT_A  1
#define COMMAND_MATCH_STATE_GOT_T  2

//
// Define useful macros
//

#ifndef ARRAY_SIZE
#define ARRAY_SIZE(x) (sizeof(x) / sizeof(x[0]))
#endif

#define MAXULONG    0xffffffff

typedef struct _QUEUE_CONTEXT
{
    WDFQUEUE        Queue;              // 默认并行队列

    WDFQUEUE        ReadQueue;          // 挂起读请求的手动队列

    WDFQUEUE        WaitMaskQueue;      // 挂起串口事件等待的手动队列

    PDEVICE_CONTEXT DeviceContext;      // 所属设备上下文指针

} QUEUE_CONTEXT, *PQUEUE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(QUEUE_CONTEXT, GetQueueContext);

EVT_WDF_IO_QUEUE_IO_READ            EvtIoRead;
EVT_WDF_IO_QUEUE_IO_WRITE           EvtIoWrite;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL  EvtIoDeviceControl;
EVT_WDF_IO_QUEUE_IO_CANCELED_ON_QUEUE EvtIoCanceledOnQueue;

NTSTATUS
QueueCreate(
    _In_  PDEVICE_CONTEXT   DeviceContext
    );

NTSTATUS
QueueProcessWriteBytes(
    _In_  PQUEUE_CONTEXT    QueueContext,
    _In_reads_bytes_(Length)
          PUCHAR            Characters,
    _In_  size_t            Length
    );

NTSTATUS
QueueProcessGetLineControl(
    _In_  PQUEUE_CONTEXT    QueueContext,
    _In_  WDFREQUEST        Request
    );

NTSTATUS
QueueProcessSetLineControl(
    _In_  PQUEUE_CONTEXT    QueueContext,
    _In_  WDFREQUEST        Request
    );

NTSTATUS
RequestCopyFromBuffer(
    _In_  WDFREQUEST        Request,
    _In_  PVOID             SourceBuffer,
    _In_  size_t            NumBytesToCopyFrom
    );

NTSTATUS
RequestCopyToBuffer(
    _In_  WDFREQUEST        Request,
    _In_  PVOID             DestinationBuffer,
    _In_  size_t            NumBytesToCopyTo
    );


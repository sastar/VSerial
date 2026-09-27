/*++

Copyright (C) VSerial Project, All Rights Reserved.

Module Name:

    queue.c

Abstract:

    This file implements the I/O queue interface for paired virtual serial ports,
    including cross-device read/write, Null-Modem signal interlocking, and wait-on-mask events.

Environment:

    Windows Driver Framework (UMDF 2.0)

--*/

#include "internal.h"

VOID
EvtIoCanceledOnQueue(
    _In_  WDFQUEUE   Queue,
    _In_  WDFREQUEST Request
    )
{
    UNREFERENCED_PARAMETER(Queue);
    Trace(TRACE_LEVEL_INFO, "EvtIoCanceledOnQueue: Request 0x%p safely canceled", Request);
    WdfRequestComplete(Request, STATUS_CANCELLED);
}

NTSTATUS
QueueCreate(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
{
    NTSTATUS                status;
    WDFDEVICE               device = DeviceContext->Device;
    WDF_IO_QUEUE_CONFIG     queueConfig;
    WDF_OBJECT_ATTRIBUTES   queueAttributes;
    WDFQUEUE                queue;
    PQUEUE_CONTEXT          queueContext;

    //
    // 创建默认并行分发队列
    //
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(
                            &queueConfig,
                            WdfIoQueueDispatchParallel);

    queueConfig.EvtIoRead           = EvtIoRead;
    queueConfig.EvtIoWrite          = EvtIoWrite;
    queueConfig.EvtIoDeviceControl  = EvtIoDeviceControl;

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(
                            &queueAttributes,
                            QUEUE_CONTEXT);

    status = WdfIoQueueCreate(
                            device,
                            &queueConfig,
                            &queueAttributes,
                            &queue);

    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfIoQueueCreate default queue failed 0x%x", status);
        return status;
    }

    queueContext = GetQueueContext(queue);
    queueContext->Queue = queue;
    queueContext->DeviceContext = DeviceContext;

    //
    // 创建挂起未决读取请求的手动队列
    //
    WDF_IO_QUEUE_CONFIG_INIT(
                            &queueConfig,
                            WdfIoQueueDispatchManual);
    queueConfig.EvtIoCanceledOnQueue = EvtIoCanceledOnQueue;

    status = WdfIoQueueCreate(
                            device,
                            &queueConfig,
                            WDF_NO_OBJECT_ATTRIBUTES,
                            &queue);

    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfIoQueueCreate manual read queue failed 0x%x", status);
        return status;
    }

    queueContext->ReadQueue = queue;
    DeviceContext->ReadQueue = queue;

    //
    // 创建挂起 IOCTL_SERIAL_WAIT_ON_MASK 请求的手动队列
    //
    WDF_IO_QUEUE_CONFIG_INIT(
                            &queueConfig,
                            WdfIoQueueDispatchManual);
    queueConfig.EvtIoCanceledOnQueue = EvtIoCanceledOnQueue;

    status = WdfIoQueueCreate(
                            device,
                            &queueConfig,
                            WDF_NO_OBJECT_ATTRIBUTES,
                            &queue);

    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfIoQueueCreate manual wait-mask queue failed 0x%x", status);
        return status;
    }

    queueContext->WaitMaskQueue = queue;
    DeviceContext->WaitMaskQueue = queue;

    return status;
}

NTSTATUS
RequestCopyFromBuffer(
    _In_  WDFREQUEST        Request,
    _In_  PVOID             SourceBuffer,
    _In_  size_t            NumBytesToCopyFrom
    )
{
    NTSTATUS                status;
    WDFMEMORY               memory;

    status = WdfRequestRetrieveOutputMemory(Request, &memory);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfRequestRetrieveOutputMemory failed 0x%x", status);
        return status;
    }

    status = WdfMemoryCopyFromBuffer(memory, 0,
                            SourceBuffer, NumBytesToCopyFrom);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfMemoryCopyFromBuffer failed 0x%x", status);
        return status;
    }

    WdfRequestSetInformation(Request, NumBytesToCopyFrom);
    return status;
}

NTSTATUS
RequestCopyToBuffer(
    _In_  WDFREQUEST        Request,
    _In_  PVOID             DestinationBuffer,
    _In_  size_t            NumBytesToCopyTo
    )
{
    NTSTATUS                status;
    WDFMEMORY               memory;

    status = WdfRequestRetrieveInputMemory(Request, &memory);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfRequestRetrieveInputMemory failed 0x%x", status);
        return status;
    }

    status = WdfMemoryCopyToBuffer(memory, 0,
                            DestinationBuffer, NumBytesToCopyTo);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfMemoryCopyToBuffer failed 0x%x", status);
        return status;
    }

    return status;
}

VOID
EvtIoWrite(
    _In_  WDFQUEUE          Queue,
    _In_  WDFREQUEST        Request,
    _In_  size_t            Length
    )
{
    NTSTATUS                status;
    PQUEUE_CONTEXT          queueContext = GetQueueContext(Queue);
    PDEVICE_CONTEXT         deviceContext = queueContext->DeviceContext;
    PSERIAL_PAIR_CHANNEL    channel = deviceContext->PairChannel;
    WDFMEMORY               memory;
    PUCHAR                  writeBuffer;
    PRING_BUFFER            txBuffer;
    PDEVICE_CONTEXT         peerDevice;

    if (Length == 0) {
        WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, 0);
        return;
    }

    status = WdfRequestRetrieveInputMemory(Request, &memory);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfRequestRetrieveInputMemory failed 0x%x", status);
        WdfRequestComplete(Request, status);
        return;
    }

    writeBuffer = (PUCHAR)WdfMemoryGetBuffer(memory, NULL);

    // 若尚未配对，直接丢弃数据并成功返回，模拟开路状态
    if (channel == NULL) {
        WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, Length);
        return;
    }

    PairChannel_Lock(channel);

    txBuffer = PairChannel_GetTxBuffer(deviceContext);
    if (txBuffer != NULL) {
        status = RingBufferWrite(txBuffer, writeBuffer, Length);
    }

    // 获取对端设备指针
    if (deviceContext->PairRole == PAIR_ROLE_A) {
        peerDevice = channel->DeviceB;
    } else {
        peerDevice = channel->DeviceA;
    }

    // 若对端设备在线，尝试唤醒对端未决的读请求
    if (peerDevice != NULL && txBuffer != NULL) {
        PRING_BUFFER peerRxBuffer = txBuffer; // A的Tx即B的Rx
        WDFREQUEST peerReadRequest;
        size_t availableData = 0;

        for (;;) {
            RingBufferGetAvailableData(peerRxBuffer, &availableData);
            if (availableData == 0) {
                break;
            }

            status = WdfIoQueueRetrieveNextRequest(peerDevice->ReadQueue, &peerReadRequest);
            if (!NT_SUCCESS(status)) {
                break;
            }

            WDFMEMORY peerOutMemory;
            status = WdfRequestRetrieveOutputMemory(peerReadRequest, &peerOutMemory);
            if (NT_SUCCESS(status)) {
                size_t reqBufSize = 0;
                PVOID pPeerBuf = WdfMemoryGetBuffer(peerOutMemory, &reqBufSize);
                size_t bytesCopied = 0;

                RingBufferRead(peerRxBuffer, (BYTE*)pPeerBuf, reqBufSize, &bytesCopied);
                WdfRequestCompleteWithInformation(peerReadRequest, STATUS_SUCCESS, bytesCopied);
            } else {
                WdfRequestComplete(peerReadRequest, status);
            }
        }

        // 通知对端收到字符事件 (SERIAL_EV_RXCHAR)
        PairChannel_NotifyRxChar(peerDevice);
    }

    PairChannel_Unlock(channel);

    // 完成本端写请求
    WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, Length);
}

VOID
EvtIoRead(
    _In_  WDFQUEUE          Queue,
    _In_  WDFREQUEST        Request,
    _In_  size_t            Length
    )
{
    NTSTATUS                status;
    PQUEUE_CONTEXT          queueContext = GetQueueContext(Queue);
    PDEVICE_CONTEXT         deviceContext = queueContext->DeviceContext;
    PSERIAL_PAIR_CHANNEL    channel = deviceContext->PairChannel;
    WDFMEMORY               memory;
    PVOID                   readBuffer;
    PRING_BUFFER            rxBuffer;
    size_t                  availableData = 0;
    size_t                  bytesCopied = 0;

    if (Length == 0) {
        WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, 0);
        return;
    }

    status = WdfRequestRetrieveOutputMemory(Request, &memory);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfRequestRetrieveOutputMemory failed 0x%x", status);
        WdfRequestComplete(Request, status);
        return;
    }

    readBuffer = WdfMemoryGetBuffer(memory, NULL);

    // 若未绑定通道，先将读请求挂入手动队列等待
    if (channel == NULL) {
        status = WdfRequestForwardToIoQueue(Request, queueContext->ReadQueue);
        if (!NT_SUCCESS(status)) {
            Trace(TRACE_LEVEL_ERROR,
                "Error: WdfRequestForwardToIoQueue failed 0x%x", status);
            WdfRequestComplete(Request, status);
        }
        return;
    }

    PairChannel_Lock(channel);

    rxBuffer = PairChannel_GetRxBuffer(deviceContext);
    if (rxBuffer != NULL) {
        RingBufferGetAvailableData(rxBuffer, &availableData);
    }

    if (availableData > 0 && rxBuffer != NULL) {
        // 缓冲区有数据，立即读取并完成请求
        status = RingBufferRead(rxBuffer, (BYTE*)readBuffer, Length, &bytesCopied);
        PairChannel_Unlock(channel);

        WdfRequestCompleteWithInformation(Request, status, bytesCopied);
        return;
    }

    PairChannel_Unlock(channel);

    // 缓冲区暂无数据，将读请求挂入 Manual ReadQueue 挂起，等待对端写入唤醒
    status = WdfRequestForwardToIoQueue(Request, queueContext->ReadQueue);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfRequestForwardToIoQueue failed 0x%x", status);
        WdfRequestComplete(Request, status);
    }
}

VOID
EvtIoDeviceControl(
    _In_  WDFQUEUE          Queue,
    _In_  WDFREQUEST        Request,
    _In_  size_t            OutputBufferLength,
    _In_  size_t            InputBufferLength,
    _In_  ULONG             IoControlCode
    )
{
    NTSTATUS                status = STATUS_SUCCESS;
    PQUEUE_CONTEXT          queueContext = GetQueueContext(Queue);
    PDEVICE_CONTEXT         deviceContext = queueContext->DeviceContext;
    UNREFERENCED_PARAMETER  (OutputBufferLength);
    UNREFERENCED_PARAMETER  (InputBufferLength);

    switch (IoControlCode)
    {
    case IOCTL_SERIAL_SET_BAUD_RATE:
    {
        SERIAL_BAUD_RATE baudRateBuffer = {0};
        status = RequestCopyToBuffer(Request, &baudRateBuffer, sizeof(baudRateBuffer));
        if (NT_SUCCESS(status)) {
            SetBaudRate(deviceContext, baudRateBuffer.BaudRate);
        }
        break;
    }

    case IOCTL_SERIAL_GET_BAUD_RATE:
    {
        SERIAL_BAUD_RATE baudRateBuffer = {0};
        baudRateBuffer.BaudRate = GetBaudRate(deviceContext);
        status = RequestCopyFromBuffer(Request, &baudRateBuffer, sizeof(baudRateBuffer));
        break;
    }

    case IOCTL_SERIAL_SET_MODEM_CONTROL:
    {
        ULONG *modemControlRegister = GetModemControlRegisterPtr(deviceContext);
        ASSERT(modemControlRegister);

        status = RequestCopyToBuffer(Request, modemControlRegister, sizeof(ULONG));
        if (NT_SUCCESS(status)) {
            PairChannel_UpdateModemLines(deviceContext, *modemControlRegister);
        }
        break;
    }

    case IOCTL_SERIAL_GET_MODEM_CONTROL:
    {
        ULONG *modemControlRegister = GetModemControlRegisterPtr(deviceContext);
        ASSERT(modemControlRegister);

        status = RequestCopyFromBuffer(Request, modemControlRegister, sizeof(ULONG));
        break;
    }

    case IOCTL_SERIAL_SET_RTS:
    {
        deviceContext->ModemControlRegister |= SERIAL_MCR_RTS;
        PairChannel_UpdateModemLines(deviceContext, deviceContext->ModemControlRegister);
        status = STATUS_SUCCESS;
        break;
    }

    case IOCTL_SERIAL_CLR_RTS:
    {
        deviceContext->ModemControlRegister &= ~SERIAL_MCR_RTS;
        PairChannel_UpdateModemLines(deviceContext, deviceContext->ModemControlRegister);
        status = STATUS_SUCCESS;
        break;
    }

    case IOCTL_SERIAL_SET_DTR:
    {
        deviceContext->ModemControlRegister |= SERIAL_MCR_DTR;
        PairChannel_UpdateModemLines(deviceContext, deviceContext->ModemControlRegister);
        status = STATUS_SUCCESS;
        break;
    }

    case IOCTL_SERIAL_CLR_DTR:
    {
        deviceContext->ModemControlRegister &= ~SERIAL_MCR_DTR;
        PairChannel_UpdateModemLines(deviceContext, deviceContext->ModemControlRegister);
        status = STATUS_SUCCESS;
        break;
    }

    case IOCTL_SERIAL_GET_MODEMSTATUS:
    {
        ULONG modemStatus = deviceContext->ModemStatus;
        status = RequestCopyFromBuffer(Request, &modemStatus, sizeof(modemStatus));
        break;
    }

    case IOCTL_SERIAL_GET_COMMSTATUS:
    {
        SERIAL_STATUS serialStatus = {0};
        size_t availableData = 0;

        if (deviceContext->PairChannel != NULL) {
            PRING_BUFFER rxBuf = PairChannel_GetRxBuffer(deviceContext);
            if (rxBuf != NULL) {
                RingBufferGetAvailableData(rxBuf, &availableData);
            }
        }

        serialStatus.AmountInInQueue = (ULONG)availableData;
        serialStatus.AmountInOutQueue = 0;
        serialStatus.Errors = 0;
        serialStatus.HoldReasons = 0;

        status = RequestCopyFromBuffer(Request, &serialStatus, sizeof(serialStatus));
        break;
    }

    case IOCTL_SERIAL_SET_FIFO_CONTROL:
    {
        ULONG *fifoControlRegister = GetFifoControlRegisterPtr(deviceContext);
        ASSERT(fifoControlRegister);

        status = RequestCopyToBuffer(Request, fifoControlRegister, sizeof(ULONG));
        break;
    }

    case IOCTL_SERIAL_GET_LINE_CONTROL:
    {
        status = QueueProcessGetLineControl(queueContext, Request);
        break;
    }

    case IOCTL_SERIAL_SET_LINE_CONTROL:
    {
        status = QueueProcessSetLineControl(queueContext, Request);
        break;
    }

    case IOCTL_SERIAL_GET_TIMEOUTS:
    {
        SERIAL_TIMEOUTS timeoutValues = {0};
        status = RequestCopyFromBuffer(Request, (void*)&timeoutValues, sizeof(timeoutValues));
        break;
    }

    case IOCTL_SERIAL_SET_TIMEOUTS:
    {
        SERIAL_TIMEOUTS timeoutValues = {0};
        status = RequestCopyToBuffer(Request, (void*)&timeoutValues, sizeof(timeoutValues));

        if (NT_SUCCESS(status)) {
            if ((timeoutValues.ReadIntervalTimeout        == MAXULONG) &&
                (timeoutValues.ReadTotalTimeoutMultiplier == MAXULONG) &&
                (timeoutValues.ReadTotalTimeoutConstant   == MAXULONG)) {
                status = STATUS_INVALID_PARAMETER;
            }
        }

        if (NT_SUCCESS(status)) {
            SetTimeouts(deviceContext, timeoutValues);
        }
        break;
    }

    case IOCTL_SERIAL_SET_WAIT_MASK:
    {
        ULONG newMask = 0;
        status = RequestCopyToBuffer(Request, &newMask, sizeof(ULONG));
        if (NT_SUCCESS(status)) {
            deviceContext->WaitMask = newMask;
            deviceContext->HistoryEvents = 0;

            // 若之前已有等待请求在队列中，按规范以返回 0 结束它
            WDFREQUEST savedRequest;
            if (NT_SUCCESS(WdfIoQueueRetrieveNextRequest(queueContext->WaitMaskQueue, &savedRequest))) {
                ULONG zeroEvent = 0;
                RequestCopyFromBuffer(savedRequest, &zeroEvent, sizeof(ULONG));
                WdfRequestComplete(savedRequest, STATUS_SUCCESS);
            }
        }
        break;
    }

    case IOCTL_SERIAL_GET_WAIT_MASK:
    {
        ULONG currentMask = deviceContext->WaitMask;
        status = RequestCopyFromBuffer(Request, &currentMask, sizeof(currentMask));
        break;
    }

    case IOCTL_SERIAL_WAIT_ON_MASK:
    {
        // 检查是否有匹配的历史事件已发生
        ULONG triggered = deviceContext->HistoryEvents & deviceContext->WaitMask;
        if (triggered != 0) {
            deviceContext->HistoryEvents &= ~triggered;
            status = RequestCopyFromBuffer(Request, &triggered, sizeof(ULONG));
            WdfRequestComplete(Request, status);
            return;
        }

        // 先清理旧请求（保证最多一个 Wait 请求排队）
        WDFREQUEST savedRequest;
        if (NT_SUCCESS(WdfIoQueueRetrieveNextRequest(queueContext->WaitMaskQueue, &savedRequest))) {
            WdfRequestComplete(savedRequest, STATUS_UNSUCCESSFUL);
        }

        // 挂入手动队列，等待事件发生时唤醒
        status = WdfRequestForwardToIoQueue(Request, queueContext->WaitMaskQueue);
        if (!NT_SUCCESS(status)) {
            Trace(TRACE_LEVEL_ERROR,
                "Error: WdfRequestForwardToIoQueue failed 0x%x", status);
            WdfRequestComplete(Request, status);
        }
        return;
    }

    case IOCTL_SERIAL_PURGE:
    {
        ULONG purgeMask = 0;
        status = RequestCopyToBuffer(Request, &purgeMask, sizeof(ULONG));
        if (NT_SUCCESS(status)) {
            if (deviceContext->PairChannel != NULL) {
                PairChannel_Lock(deviceContext->PairChannel);
                if (purgeMask & SERIAL_PURGE_RXCLEAR) {
                    PRING_BUFFER rxBuf = PairChannel_GetRxBuffer(deviceContext);
                    if (rxBuf != NULL) {
                        RingBufferInitialize(rxBuf, rxBuf->Base, rxBuf->Size);
                    }
                }
                if (purgeMask & SERIAL_PURGE_TXCLEAR) {
                    PRING_BUFFER txBuf = PairChannel_GetTxBuffer(deviceContext);
                    if (txBuf != NULL) {
                        RingBufferInitialize(txBuf, txBuf->Base, txBuf->Size);
                    }
                }
                PairChannel_Unlock(deviceContext->PairChannel);
            }

            // 取消挂起的读请求
            if (purgeMask & SERIAL_PURGE_RXABORT) {
                WDFREQUEST abortReq;
                while (NT_SUCCESS(WdfIoQueueRetrieveNextRequest(queueContext->ReadQueue, &abortReq))) {
                    WdfRequestComplete(abortReq, STATUS_CANCELLED);
                }
            }
        }
        break;
    }

    case IOCTL_SERIAL_GET_PROPERTIES:
    {
        SERIAL_COMMPROP commProp = {0};

        commProp.PacketLength = sizeof(SERIAL_COMMPROP);
        commProp.PacketVersion = 2;
        commProp.ServiceMask = SERIAL_SP_SERIALCOMM;
        commProp.MaxTxQueue = PAIR_BUFFER_SIZE;
        commProp.MaxRxQueue = PAIR_BUFFER_SIZE;
        commProp.MaxBaud = SERIAL_BAUD_USER;
        commProp.ProvSubType = SERIAL_SP_RS232;

        commProp.ProvCapabilities =
            SERIAL_PCF_DTRDSR        |
            SERIAL_PCF_RTSCTS        |
            SERIAL_PCF_CD            |
            SERIAL_PCF_PARITY_CHECK  |
            SERIAL_PCF_XONXOFF       |
            SERIAL_PCF_SETXCHAR      |
            SERIAL_PCF_TOTALTIMEOUTS |
            SERIAL_PCF_INTTIMEOUTS;

        commProp.SettableParams =
            SERIAL_SP_PARITY         |
            SERIAL_SP_BAUD           |
            SERIAL_SP_DATABITS       |
            SERIAL_SP_STOPBITS       |
            SERIAL_SP_HANDSHAKING    |
            SERIAL_SP_PARITY_CHECK   |
            SERIAL_SP_CARRIER_DETECT;

        commProp.SettableBaud =
            SERIAL_BAUD_075          |
            SERIAL_BAUD_110          |
            SERIAL_BAUD_134_5        |
            SERIAL_BAUD_150          |
            SERIAL_BAUD_300          |
            SERIAL_BAUD_600          |
            SERIAL_BAUD_1200         |
            SERIAL_BAUD_1800         |
            SERIAL_BAUD_2400         |
            SERIAL_BAUD_4800         |
            SERIAL_BAUD_7200         |
            SERIAL_BAUD_9600         |
            SERIAL_BAUD_14400        |
            SERIAL_BAUD_19200        |
            SERIAL_BAUD_38400        |
            SERIAL_BAUD_56K          |
            SERIAL_BAUD_128K         |
            SERIAL_BAUD_115200       |
            SERIAL_BAUD_57600        |
            SERIAL_BAUD_USER;

        commProp.SettableData =
            SERIAL_DATABITS_5        |
            SERIAL_DATABITS_6        |
            SERIAL_DATABITS_7        |
            SERIAL_DATABITS_8;

        commProp.SettableStopParity =
            SERIAL_STOPBITS_10       |
            SERIAL_STOPBITS_15       |
            SERIAL_STOPBITS_20       |
            SERIAL_PARITY_NONE       |
            SERIAL_PARITY_ODD        |
            SERIAL_PARITY_EVEN       |
            SERIAL_PARITY_MARK       |
            SERIAL_PARITY_SPACE;

        commProp.CurrentTxQueue = 0;
        commProp.CurrentRxQueue = PAIR_BUFFER_SIZE;

        status = RequestCopyFromBuffer(Request, &commProp, sizeof(SERIAL_COMMPROP));
        break;
    }

    case IOCTL_SERIAL_GET_HANDFLOW:
    {
        status = RequestCopyFromBuffer(Request, &deviceContext->HandFlow, sizeof(SERIAL_HANDFLOW));
        break;
    }

    case IOCTL_SERIAL_SET_HANDFLOW:
    {
        SERIAL_HANDFLOW newHandflow = {0};
        status = RequestCopyToBuffer(Request, &newHandflow, sizeof(SERIAL_HANDFLOW));
        if (NT_SUCCESS(status)) {
            deviceContext->HandFlow = newHandflow;
        }
        break;
    }

    case IOCTL_SERIAL_GET_CHARS:
    {
        status = RequestCopyFromBuffer(Request, &deviceContext->SpecialChars, sizeof(SERIAL_CHARS));
        break;
    }

    case IOCTL_SERIAL_SET_CHARS:
    {
        SERIAL_CHARS newChars = {0};
        status = RequestCopyToBuffer(Request, &newChars, sizeof(SERIAL_CHARS));
        if (NT_SUCCESS(status)) {
            deviceContext->SpecialChars = newChars;
        }
        break;
    }

    case IOCTL_SERIAL_CONFIG_SIZE:
    {
        ULONG configSize = 0;
        status = RequestCopyFromBuffer(Request, &configSize, sizeof(ULONG));
        break;
    }

    case IOCTL_SERIAL_GET_DTRRTS:
    {
        ULONG dtrRts = 0;
        if (deviceContext->ModemControlRegister & SERIAL_MCR_DTR) {
            dtrRts |= SERIAL_DTR_STATE;
        }
        if (deviceContext->ModemControlRegister & SERIAL_MCR_RTS) {
            dtrRts |= SERIAL_RTS_STATE;
        }
        status = RequestCopyFromBuffer(Request, &dtrRts, sizeof(ULONG));
        break;
    }

    case IOCTL_SERIAL_SET_QUEUE_SIZE:
    case IOCTL_SERIAL_SET_XON:
    case IOCTL_SERIAL_SET_XOFF:
    case IOCTL_SERIAL_RESET_DEVICE:
        // 虚拟串口兼容性返回成功
        status = STATUS_SUCCESS;
        break;

    default:
        status = STATUS_INVALID_PARAMETER;
        break;
    }

    WdfRequestComplete(Request, status);
}

NTSTATUS
QueueProcessGetLineControl(
    _In_  PQUEUE_CONTEXT    QueueContext,
    _In_  WDFREQUEST        Request
    )
{
    NTSTATUS                status;
    PDEVICE_CONTEXT         deviceContext;
    SERIAL_LINE_CONTROL     lineControl = {0};
    ULONG                   lineControlSnapshot;
    ULONG                   *lineControlRegister;

    deviceContext = QueueContext->DeviceContext;
    lineControlRegister = GetLineControlRegisterPtr(deviceContext);

    ASSERT(lineControlRegister);

    lineControlSnapshot = ReadNoFence((LONG *)lineControlRegister);

    // 数据位
    if ((lineControlSnapshot & SERIAL_DATA_MASK) == SERIAL_5_DATA) {
        lineControl.WordLength = 5;
    } else if ((lineControlSnapshot & SERIAL_DATA_MASK) == SERIAL_6_DATA) {
        lineControl.WordLength = 6;
    } else if ((lineControlSnapshot & SERIAL_DATA_MASK) == SERIAL_7_DATA) {
        lineControl.WordLength = 7;
    } else if ((lineControlSnapshot & SERIAL_DATA_MASK) == SERIAL_8_DATA) {
        lineControl.WordLength = 8;
    }

    // 校验位
    if ((lineControlSnapshot & SERIAL_PARITY_MASK) == SERIAL_NONE_PARITY) {
        lineControl.Parity = NO_PARITY;
    } else if ((lineControlSnapshot & SERIAL_PARITY_MASK) == SERIAL_ODD_PARITY) {
        lineControl.Parity = ODD_PARITY;
    } else if ((lineControlSnapshot & SERIAL_PARITY_MASK) == SERIAL_EVEN_PARITY) {
        lineControl.Parity = EVEN_PARITY;
    } else if ((lineControlSnapshot & SERIAL_PARITY_MASK) == SERIAL_MARK_PARITY) {
        lineControl.Parity = MARK_PARITY;
    } else if ((lineControlSnapshot & SERIAL_PARITY_MASK) == SERIAL_SPACE_PARITY) {
        lineControl.Parity = SPACE_PARITY;
    }

    // 停止位
    if (lineControlSnapshot & SERIAL_2_STOP) {
        if (lineControl.WordLength == 5) {
            lineControl.StopBits = STOP_BITS_1_5;
        } else {
            lineControl.StopBits = STOP_BITS_2;
        }
    } else {
        lineControl.StopBits = STOP_BIT_1;
    }

    status = RequestCopyFromBuffer(Request, (void*)&lineControl, sizeof(lineControl));
    return status;
}

NTSTATUS
QueueProcessSetLineControl(
    _In_  PQUEUE_CONTEXT    QueueContext,
    _In_  WDFREQUEST        Request
    )
{
    NTSTATUS                status;
    PDEVICE_CONTEXT         deviceContext;
    SERIAL_LINE_CONTROL     lineControl = {0};
    ULONG                   *lineControlRegister;
    UCHAR                   lineControlData = 0;
    UCHAR                   lineControlStop = 0;
    UCHAR                   lineControlParity = 0;
    ULONG                   lineControlSnapshot;
    ULONG                   lineControlNew;
    ULONG                   lineControlPrevious;
    ULONG                   i;

    deviceContext = QueueContext->DeviceContext;
    lineControlRegister = GetLineControlRegisterPtr(deviceContext);

    ASSERT(lineControlRegister);

    status = RequestCopyToBuffer(Request, (void*)&lineControl, sizeof(lineControl));
    if (!NT_SUCCESS(status)) {
        return status;
    }

    // 数据位
    switch (lineControl.WordLength) {
    case 5:
        lineControlData = SERIAL_5_DATA;
        SetValidDataMask(deviceContext, 0x1f);
        break;
    case 6:
        lineControlData = SERIAL_6_DATA;
        SetValidDataMask(deviceContext, 0x3f);
        break;
    case 7:
        lineControlData = SERIAL_7_DATA;
        SetValidDataMask(deviceContext, 0x7f);
        break;
    case 8:
        lineControlData = SERIAL_8_DATA;
        SetValidDataMask(deviceContext, 0xff);
        break;
    default:
        return STATUS_INVALID_PARAMETER;
    }

    // 停止位
    switch (lineControl.StopBits) {
    case STOP_BIT_1:
        lineControlStop = SERIAL_1_STOP;
        break;
    case STOP_BITS_1_5:
        if (lineControlData != SERIAL_5_DATA) {
            return STATUS_INVALID_PARAMETER;
        }
        lineControlStop = SERIAL_1_5_STOP;
        break;
    case STOP_BITS_2:
        if (lineControlData == SERIAL_5_DATA) {
            return STATUS_INVALID_PARAMETER;
        }
        lineControlStop = SERIAL_2_STOP;
        break;
    default:
        return STATUS_INVALID_PARAMETER;
    }

    // 校验位
    switch (lineControl.Parity) {
    case NO_PARITY:
        lineControlParity = SERIAL_NONE_PARITY;
        break;
    case EVEN_PARITY:
        lineControlParity = SERIAL_EVEN_PARITY;
        break;
    case ODD_PARITY:
        lineControlParity = SERIAL_ODD_PARITY;
        break;
    case SPACE_PARITY:
        lineControlParity = SERIAL_SPACE_PARITY;
        break;
    case MARK_PARITY:
        lineControlParity = SERIAL_MARK_PARITY;
        break;
    default:
        return STATUS_INVALID_PARAMETER;
    }

    // 原子更新线控制寄存器
    i = 0;
    do {
        i++;
        if ((i & 0xf) == 0) {
            SwitchToThread();
        }

        lineControlSnapshot = ReadNoFence((LONG *)lineControlRegister);

        lineControlNew = (lineControlSnapshot & SERIAL_LCR_BREAK) |
                        (lineControlData | lineControlParity | lineControlStop);

        lineControlPrevious = InterlockedCompareExchange(
                      (LONG *) lineControlRegister,
                       lineControlNew,
                       lineControlSnapshot);

    } while (lineControlPrevious != lineControlSnapshot);

    return STATUS_SUCCESS;
}

/*++

Copyright (C) Microsoft Corporation, All Rights Reserved.

Module Name:

    Device.c

Abstract:

    This module contains the implementation of the VirtualSerial sample
    driver's device callback object.

    The VirtualSerial sample device does very little.  It does not implement
    either of the PNP interfaces so once the device is setup, it won't ever get
    any callbacks until the device is removed.

Environment:

    Windows Driver Framework

--*/

#include "internal.h"

NTSTATUS
DeviceCreate(
    _In_  WDFDRIVER         Driver,
    _In_  PWDFDEVICE_INIT   DeviceInit,
    _Out_ PDEVICE_CONTEXT   *DeviceContext
    )
/*++

  Routine Description:

    This method creates and initializs an instance of the VirtualSerial driver's
    device callback object.

  Arguments:

    FxDeviceInit - the settings for the device.

    Device - a location to store the referenced pointer to the device object.

  Return Value:

    Status

--*/
{
    NTSTATUS                status;
    WDF_OBJECT_ATTRIBUTES   deviceAttributes;
    WDFDEVICE               device;
    PDEVICE_CONTEXT         deviceContext;
    WDF_FILEOBJECT_CONFIG   fileConfig;
    UNREFERENCED_PARAMETER  (Driver);

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(
                            &deviceAttributes,
                            DEVICE_CONTEXT);

    deviceAttributes.SynchronizationScope = WdfSynchronizationScopeDevice;
    deviceAttributes.EvtCleanupCallback   = EvtDeviceCleanup;

    // 配置文件对象生命周期回调，确保 CloseHandle 时自动撤回未决请求
    WDF_FILEOBJECT_CONFIG_INIT(
                            &fileConfig,
                            WDF_NO_EVENT_CALLBACK,
                            EvtFileClose,
                            EvtFileCleanup);

    WdfDeviceInitSetFileObjectConfig(
                            DeviceInit,
                            &fileConfig,
                            WDF_NO_OBJECT_ATTRIBUTES);

    status = WdfDeviceCreate(&DeviceInit,
                            &deviceAttributes,
                            &device);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: WdfDeviceCreate failed 0x%x", status);
        return status;
    }

    deviceContext = GetDeviceContext(device);
    deviceContext->Device = device;

    // 初始化流控与特殊字符默认值
    deviceContext->HandFlow.ControlHandShake = SERIAL_DTR_CONTROL;
    deviceContext->HandFlow.FlowReplace      = SERIAL_RTS_CONTROL;
    deviceContext->HandFlow.XonLimit         = PAIR_BUFFER_SIZE / 2;
    deviceContext->HandFlow.XoffLimit        = PAIR_BUFFER_SIZE / 4;

    deviceContext->SpecialChars.XonChar      = 0x11;
    deviceContext->SpecialChars.XoffChar     = 0x13;

    *DeviceContext = deviceContext;

    return status;
}


NTSTATUS
DeviceConfigure(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
/*++

  Routine Description:

    This method is called after the device callback object has been initialized
    and returned to the driver.  It would setup the device's queues and their
    corresponding callback objects.

  Arguments:

    FxDevice - the framework device object for which we're handling events.

  Return Value:

    status

--*/
{
    NTSTATUS                status;
    WDFDEVICE               device = DeviceContext->Device;
    WDFKEY                  key = NULL;
    LPGUID                  guid;
    errno_t                 errorNo;
    
    DECLARE_CONST_UNICODE_STRING(portName,          REG_VALUENAME_PORTNAME);
    DECLARE_UNICODE_STRING_SIZE (comPort,           10);
    DECLARE_UNICODE_STRING_SIZE (symbolicLinkName,  SYMBOLIC_LINK_NAME_LENGTH);

#ifdef _FAKE_MODEM
    //
    // Compiled as fake modem
    //
    guid = (LPGUID) &GUID_DEVINTERFACE_MODEM;
#else
    //
    // Compiled as virtual serial port
    //
    guid = (LPGUID) &GUID_DEVINTERFACE_COMPORT;
#endif

    //
    // Create device interface
    //
    status = WdfDeviceCreateDeviceInterface(
                            device,
                            guid,
                            NULL);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Cannot create device interface");
        goto Exit;
    }

    //
    // Read the COM port number from the registry, which has been automatically
    // created by "MsPorts!PortsClassInstaller" if INF file says "Class=Ports"
    //
    status = WdfDeviceOpenRegistryKey(
                            device,
                            PLUGPLAY_REGKEY_DEVICE,
                            KEY_QUERY_VALUE,
                            WDF_NO_OBJECT_ATTRIBUTES,
                            &key);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Failed to retrieve device hardware key root");
        goto Exit;
    }

    status = WdfRegistryQueryUnicodeString(
                            key,
                            &portName,
                            NULL,
                            &comPort);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Failed to read PortName");
        goto Exit;
    }
        
    DECLARE_CONST_UNICODE_STRING(pairIdValName,     L"PairId");
    DECLARE_CONST_UNICODE_STRING(pairRoleValName,   L"PairRole");
    ULONG                   pairId = 1;
    ULONG                   pairRole = PAIR_ROLE_A;

    // 读取配对 ID 与角色（若注册表中不存在则使用默认值）
    WdfRegistryQueryULong(key, &pairIdValName, &pairId);
    WdfRegistryQueryULong(key, &pairRoleValName, &pairRole);

    wcsncpy_s(DeviceContext->PortName,
              sizeof(DeviceContext->PortName) / sizeof(WCHAR),
              comPort.Buffer,
              _TRUNCATE);

    // 关闭设备注册表句柄
    WdfRegistryClose(key);
    key = NULL;

    //
    // Manually create the symbolic link name. Length is the length in
    // bytes not including the NULL terminator.
    //
    // 6054 and 26035 are code analysis warnings that comPort.Buffer might
    // not be NULL terminated, while we know that they are. 
    //
    #pragma warning(suppress: 6054 26035)
    symbolicLinkName.Length = (USHORT)((wcslen(comPort.Buffer) * sizeof(wchar_t))
                                + sizeof(SYMBOLIC_LINK_NAME_PREFIX) - sizeof(UNICODE_NULL));
                                
    if (symbolicLinkName.Length >= symbolicLinkName.MaximumLength) {
        
        Trace(TRACE_LEVEL_ERROR, "Error: Buffer overflow when creating COM port name. Size"
            " is %d, buffer length is %d", symbolicLinkName.Length, symbolicLinkName.MaximumLength);
        status = STATUS_BUFFER_OVERFLOW;
        goto Exit;
    }
        
    errorNo = wcscpy_s(symbolicLinkName.Buffer,
                       SYMBOLIC_LINK_NAME_LENGTH,
                       SYMBOLIC_LINK_NAME_PREFIX);
                            
    if (errorNo != 0) {
        Trace(TRACE_LEVEL_ERROR, 
              "Failed to copy %ws to buffer with error %d",
              SYMBOLIC_LINK_NAME_PREFIX, errorNo);
        status = STATUS_INVALID_PARAMETER;
        goto Exit;
    }
        
    errorNo = wcscat_s(symbolicLinkName.Buffer,
                       SYMBOLIC_LINK_NAME_LENGTH,
                       comPort.Buffer);
                            
    if (errorNo != 0) {
        Trace(TRACE_LEVEL_ERROR, 
              "Failed to copy %ws to buffer with error %d",
              comPort.Buffer, errorNo);
        status = STATUS_INVALID_PARAMETER;
        goto Exit;
    }
                                
    //
    // Create symbolic link
    //
    status = WdfDeviceCreateSymbolicLink(
                            device,
                            &symbolicLinkName);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Cannot create symbolic link %ws", symbolicLinkName.Buffer);
        goto Exit;
    }

    status = DeviceGetPdoName(DeviceContext);
    if (!NT_SUCCESS(status)) {
        goto Exit;
    }

    status = DeviceWriteLegacyHardwareKey(
                            DeviceContext->PdoName,
                            comPort.Buffer,
                            DeviceContext->Device);
    if (NT_SUCCESS(status)) {
        DeviceContext->CreatedLegacyHardwareKey = TRUE;
    }

    status = QueueCreate(DeviceContext);
    if (!NT_SUCCESS(status)) {
        goto Exit;
    }

    // 注册设备至配对通道
    status = PairManager_RegisterDevice(DeviceContext, pairId, pairRole);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: PairManager_RegisterDevice failed 0x%x", status);
        goto Exit;
    }

Exit:
    if (key != NULL) {
        WdfRegistryClose(key);
        key = NULL;
    }
    return status;
}


NTSTATUS
DeviceGetPdoName(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
{
    NTSTATUS                status;
    WDFDEVICE               device = DeviceContext->Device;
    WDF_OBJECT_ATTRIBUTES   attributes;
    WDFMEMORY               memory;

    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ParentObject = device;

    status = WdfDeviceAllocAndQueryProperty(
                            device,
                            DevicePropertyPhysicalDeviceObjectName,
                            NonPagedPoolNx,
                            &attributes,
                            &memory);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Failed to query PDO name");
        goto Exit;
    }

    DeviceContext->PdoName = (PWCHAR) WdfMemoryGetBuffer(memory, NULL);
    Trace(TRACE_LEVEL_ERROR,
            "PDO Name is %ws", DeviceContext->PdoName);

Exit:
    return status;
}


NTSTATUS
DeviceWriteLegacyHardwareKey(
    _In_  PWSTR             PdoName,
    _In_  PWSTR             ComPort,
    _In_  WDFDEVICE         Device
    )
{
    WDFKEY                  key = NULL;
    NTSTATUS                status;
    UNICODE_STRING          pdoString = {0};
    UNICODE_STRING          comPort = {0};
    
    DECLARE_CONST_UNICODE_STRING(deviceSubkey, SERIAL_DEVICE_MAP);
                
    RtlInitUnicodeString(&pdoString, PdoName);  
    RtlInitUnicodeString(&comPort, ComPort);
   
    status = WdfDeviceOpenDevicemapKey(Device,
                                       &deviceSubkey,
                                       KEY_SET_VALUE,
                                       WDF_NO_OBJECT_ATTRIBUTES,
                                       &key);
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Failed to open DEVICEMAP\\SERIALCOMM key 0x%x", status);
        goto exit;
    }
    
    status = WdfRegistryAssignUnicodeString(key,
                                            &pdoString,
                                            &comPort);
                                            
    if (!NT_SUCCESS(status)) {
        Trace(TRACE_LEVEL_ERROR,
            "Error: Failed to write to DEVICEMAP\\SERIALCOMM key 0x%x", status);
        goto exit;
    }                                 

exit:

    if (key != NULL) {
        WdfRegistryClose(key);
        key = NULL;
    }

    return status;
}


VOID
EvtDeviceCleanup(
    _In_  WDFOBJECT         Object
    )
{
    WDFDEVICE               device = (WDFDEVICE) Object;
    PDEVICE_CONTEXT         deviceContext = GetDeviceContext(device);
    NTSTATUS                status;
    WDFKEY                  key = NULL;
    UNICODE_STRING          pdoString = {0};
    
    DECLARE_CONST_UNICODE_STRING(deviceSubkey, SERIAL_DEVICE_MAP);

    // 从配对通道管理器中注销
    PairManager_UnregisterDevice(deviceContext);

    if (deviceContext->CreatedLegacyHardwareKey == TRUE) {
    
        RtlInitUnicodeString(&pdoString, deviceContext->PdoName);
    
        status = WdfDeviceOpenDevicemapKey(device,
                                           &deviceSubkey,
                                           KEY_SET_VALUE,
                                           WDF_NO_OBJECT_ATTRIBUTES,
                                           &key);
                                           
        if (!NT_SUCCESS(status)) {
            Trace(TRACE_LEVEL_ERROR,
                "Error: Failed to open DEVICEMAP\\SERIALCOMM key 0x%x", status);
            goto exit;
        }
        
        status = WdfRegistryRemoveValue(key,
                                        &pdoString);
        if (!NT_SUCCESS(status)) {
            Trace(TRACE_LEVEL_ERROR,
                "Error: Failed to delete %S key, 0x%x", pdoString.Buffer, status);  
            goto exit;
        }
        
        status = WdfRegistryRemoveKey(key);
        if (!NT_SUCCESS(status)) {
            Trace(TRACE_LEVEL_ERROR,
                "Error: Failed to delete %S, 0x%x", SERIAL_DEVICE_MAP, status);  
            goto exit;
        }           
    }
        
exit:

    if (key != NULL) {
        WdfRegistryClose(key);
        key = NULL;
    }

    return;
}


ULONG
GetBaudRate(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
{
    return DeviceContext->BaudRate;
}

VOID
SetBaudRate(
    _In_  PDEVICE_CONTEXT   DeviceContext,
    _In_  ULONG             BaudRate
    )
{
    DeviceContext->BaudRate = BaudRate;
}

ULONG *
GetModemControlRegisterPtr(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
{
    return &DeviceContext->ModemControlRegister;
}

ULONG *
GetFifoControlRegisterPtr(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
{
    return &DeviceContext->FifoControlRegister;
}

ULONG *
GetLineControlRegisterPtr(
    _In_  PDEVICE_CONTEXT   DeviceContext
    )
{
    return &DeviceContext->LineControlRegister;
}

VOID
SetValidDataMask(
    _In_  PDEVICE_CONTEXT   DeviceContext,
    _In_  UCHAR             Mask
    )
{
    DeviceContext->ValidDataMask = Mask;
}

VOID
SetTimeouts(
    _In_  PDEVICE_CONTEXT   DeviceContext,
    _In_  SERIAL_TIMEOUTS   Timeouts
    )
{
    DeviceContext->Timeouts = Timeouts;
}

VOID
GetTimeouts(
    _In_  PDEVICE_CONTEXT   DeviceContext,
    _Out_ SERIAL_TIMEOUTS   *Timeouts
    )
{
    *Timeouts = DeviceContext->Timeouts;
}

VOID
EvtFileCleanup(
    _In_  WDFFILEOBJECT     FileObject
    )
{
    WDFDEVICE       device = WdfFileObjectGetDevice(FileObject);
    PDEVICE_CONTEXT deviceContext = GetDeviceContext(device);
    WDFREQUEST      req;

    Trace(TRACE_LEVEL_INFO, "EvtFileCleanup: FileObject 0x%p closing, cleaning up queues", FileObject);

    // 文件句柄关闭时，安全取消并完成挂在该队列上的未决 WaitMask 请求
    if (deviceContext->WaitMaskQueue != NULL) {
        while (NT_SUCCESS(WdfIoQueueRetrieveNextRequest(deviceContext->WaitMaskQueue, &req))) {
            WdfRequestComplete(req, STATUS_CANCELLED);
        }
    }

    // 安全取消并完成挂在该队列上的未决读取请求
    if (deviceContext->ReadQueue != NULL) {
        while (NT_SUCCESS(WdfIoQueueRetrieveNextRequest(deviceContext->ReadQueue, &req))) {
            WdfRequestComplete(req, STATUS_CANCELLED);
        }
    }
}

VOID
EvtFileClose(
    _In_  WDFFILEOBJECT     FileObject
    )
{
    UNREFERENCED_PARAMETER(FileObject);
    Trace(TRACE_LEVEL_INFO, "EvtFileClose: FileObject 0x%p closed", FileObject);
}


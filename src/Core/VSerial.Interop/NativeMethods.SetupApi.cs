using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace VSerial.Interop;

public static partial class NativeMethods
{
    private const string SETUPAPI_LIB = "setupapi.dll";

    /// <summary>
    /// 创建空的设备信息集 (SetupDiCreateDeviceInfoList)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    public static extern SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoList(
        in Guid ClassGuid,
        IntPtr hwndParent
    );

    /// <summary>
    /// 在指定的设备信息集中创建新的设备节点 (SetupDiCreateDeviceInfoW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiCreateDeviceInfoW(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        string DeviceName,
        in Guid ClassGuid,
        string? DeviceDescription,
        IntPtr hwndParent,
        uint CreationFlags,
        ref SP_DEVINFO_DATA DeviceInfoData
    );

    /// <summary>
    /// 设置设备节点的注册表硬件属性 (SetupDiSetDeviceRegistryPropertyW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiSetDeviceRegistryPropertyW(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        byte[] PropertyBuffer,
        uint PropertyBufferSize
    );

    /// <summary>
    /// 调用类安装程序或默认安装程序处理 DIF 请求 (SetupDiCallClassInstaller)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiCallClassInstaller(
        uint InstallFunction,
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData
    );

    /// <summary>
    /// 检索设备信息集或特定设备元素的安装参数 (SetupDiGetDeviceInstallParamsW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiGetDeviceInstallParamsW(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        ref SP_DEVINSTALL_PARAMS DeviceInstallParams
    );

    /// <summary>
    /// 设置设备信息集或特定设备元素的安装参数 (SetupDiSetDeviceInstallParamsW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiSetDeviceInstallParamsW(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        ref SP_DEVINSTALL_PARAMS DeviceInstallParams
    );

    /// <summary>
    /// 为指定设备创建或打开注册表配置键 (SetupDiCreateDevRegKeyW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeRegistryHandle SetupDiCreateDevRegKeyW(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Scope,
        uint HwProfile,
        uint KeyType,
        IntPtr hKeyClass,
        string? SubKey
    );

    /// <summary>
    /// 打开指定设备的硬件/软件注册表配置键 (SetupDiOpenDevRegKey)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    public static extern SafeRegistryHandle SetupDiOpenDevRegKey(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Scope,
        uint HwProfile,
        uint KeyType,
        uint samDesired
    );

    /// <summary>
    /// 构建指定设备的兼容驱动程序列表 (SetupDiBuildDriverInfoList)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiBuildDriverInfoList(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint DriverType
    );

    /// <summary>
    /// 为指定设备选择最佳兼容驱动程序 (SetupDiSelectBestCompatDrv)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiSelectBestCompatDrv(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData
    );

    /// <summary>
    /// 销毁设备信息集并释放所有分配资源 (SetupDiDestroyDeviceInfoList)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiDestroyDeviceInfoList(
        IntPtr DeviceInfoSet
    );

    /// <summary>
    /// 获取指定设备类或包含指定接口的设备信息集 (SetupDiGetClassDevsW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsW(
        in Guid ClassGuid,
        string? Enumerator,
        IntPtr hwndParent,
        uint Flags
    );

    /// <summary>
    /// 枚举设备信息集中的各个设备节点 (SetupDiEnumDeviceInfo)
    /// </summary>
    [DllImport(SETUPAPI_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiEnumDeviceInfo(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        uint MemberIndex,
        ref SP_DEVINFO_DATA DeviceInfoData
    );

    /// <summary>
    /// 读取设备节点的注册表硬件属性 (SetupDiGetDeviceRegistryPropertyW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiGetDeviceRegistryPropertyW(
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        byte[]? PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize
    );

    /// <summary>
    /// 将 OEM INF 文件预复制安装到系统驱动程序存储区 DriverStore (SetupCopyOEMInfW)
    /// </summary>
    [DllImport(SETUPAPI_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupCopyOEMInfW(
        string SourceInfFileName,
        string? OEMSourceMediaLocation,
        uint OEMSourceMediaType,
        uint CopyStyle,
        StringBuilder? DestinationInfFileName,
        uint DestinationInfFileNameSize,
        out uint RequiredSize,
        StringBuilder? DestinationInfFileNameComponent
    );
}

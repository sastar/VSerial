using System;
using System.Runtime.InteropServices;

namespace VSerial.Interop;

public static partial class NativeMethods
{
    private const string NEWDEV_LIB = "newdev.dll";

    /// <summary>
    /// 为匹配指定硬件 ID 的即插即用设备更新并重载驱动程序 (UpdateDriverForPlugAndPlayDevicesW)
    /// </summary>
    [DllImport(NEWDEV_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateDriverForPlugAndPlayDevicesW(
        IntPtr hwndParent,
        string HardwareId,
        string FullInfPath,
        uint InstallFlags,
        [MarshalAs(UnmanagedType.Bool)] out bool bRebootRequired
    );

    /// <summary>
    /// 从 DriverStore 彻底卸载指定的驱动程序包并移除所有关联设备 (DiUninstallDriverW)
    /// </summary>
    [DllImport(NEWDEV_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DiUninstallDriverW(
        IntPtr hwndParent,
        string InfPath,
        uint Flags,
        [MarshalAs(UnmanagedType.Bool)] out bool NeedReboot
    );

    /// <summary>
    /// 为指定的即插即用设备节点安装驱动程序 (DiInstallDevice)
    /// </summary>
    [DllImport(NEWDEV_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DiInstallDevice(
        IntPtr hwndParent,
        SafeDeviceInfoSetHandle DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        IntPtr DriverInfoData,
        uint Flags,
        [MarshalAs(UnmanagedType.Bool)] out bool NeedReboot
    );
}

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VSerial.Interop;

/// <summary>
/// 设备信息数据结构体 (SP_DEVINFO_DATA)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SP_DEVINFO_DATA
{
    public uint cbSize;
    public Guid ClassGuid;
    public uint DevInst;
    public IntPtr Reserved;

    /// <summary>
    /// 初始化结构体大小
    /// </summary>
    public static SP_DEVINFO_DATA Create()
    {
        return new SP_DEVINFO_DATA
        {
            cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>(),
            ClassGuid = Guid.Empty,
            DevInst = 0,
            Reserved = IntPtr.Zero
        };
    }
}

/// <summary>
/// 设备安装控制参数结构体 (SP_DEVINSTALL_PARAMS_W)
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct SP_DEVINSTALL_PARAMS
{
    public uint cbSize;
    public uint Flags;
    public uint FlagsEx;
    public IntPtr hwndParent;
    public IntPtr InstallMsgHandler;
    public IntPtr InstallMsgHandlerContext;
    public IntPtr FileQueue;
    public UIntPtr ClassInstallReserved;
    public uint Reserved;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string DriverPath;

    public static SP_DEVINSTALL_PARAMS Create()
    {
        return new SP_DEVINSTALL_PARAMS
        {
            cbSize = (uint)Marshal.SizeOf<SP_DEVINSTALL_PARAMS>(),
            DriverPath = string.Empty
        };
    }
}

/// <summary>
/// 安全的设备信息集句柄封装 (HDEVINFO)
/// 离开作用域或释放时自动调用 SetupDiDestroyDeviceInfoList 避免句柄泄漏
/// </summary>
public sealed class SafeDeviceInfoSetHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeDeviceInfoSetHandle() : base(true)
    {
    }

    public SafeDeviceInfoSetHandle(IntPtr preexistingHandle, bool ownsHandle) : base(ownsHandle)
    {
        SetHandle(preexistingHandle);
    }

    protected override bool ReleaseHandle()
    {
        return NativeMethods.SetupDiDestroyDeviceInfoList(handle);
    }
}

/// <summary>
/// 安全的证书上下文句柄封装 (PCCERT_CONTEXT)
/// 自动调用 CertFreeCertificateContext 释放证书内存
/// </summary>
public sealed class SafeCertContextHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeCertContextHandle() : base(true)
    {
    }

    public SafeCertContextHandle(IntPtr preexistingHandle, bool ownsHandle) : base(ownsHandle)
    {
        SetHandle(preexistingHandle);
    }

    protected override bool ReleaseHandle()
    {
        return NativeMethods.CertFreeCertificateContext(handle);
    }
}

/// <summary>
/// 安全的证书存储库句柄封装 (HCERTSTORE)
/// 自动调用 CertCloseStore 释放存储区
/// </summary>
public sealed class SafeCertStoreHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeCertStoreHandle() : base(true)
    {
    }

    public SafeCertStoreHandle(IntPtr preexistingHandle, bool ownsHandle) : base(ownsHandle)
    {
        SetHandle(preexistingHandle);
    }

    protected override bool ReleaseHandle()
    {
        return NativeMethods.CertCloseStore(handle, 0);
    }
}

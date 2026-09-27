using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VSerial.Interop;

public static partial class NativeMethods
{
    private const string ADVAPI32_LIB = "advapi32.dll";
    private const string KERNEL32_LIB = "kernel32.dll";

    /// <summary>
    /// 在指定注册表项下设置数据和类型 (RegSetValueExW)
    /// </summary>
    [DllImport(ADVAPI32_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RegSetValueExW(
        SafeRegistryHandle hKey,
        string lpValueName,
        uint Reserved,
        uint dwType,
        byte[] lpData,
        uint cbData
    );

    /// <summary>
    /// 检索指定注册表项下指定值名称的类型和数据 (RegQueryValueExW)
    /// </summary>
    [DllImport(ADVAPI32_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RegQueryValueExW(
        SafeRegistryHandle hKey,
        string lpValueName,
        IntPtr lpReserved,
        out uint lpType,
        byte[]? lpData,
        ref uint lpcbData
    );

    /// <summary>
    /// 关闭指定注册表项的句柄 (RegCloseKey)
    /// </summary>
    [DllImport(ADVAPI32_LIB, SetLastError = true)]
    public static extern uint RegCloseKey(
        IntPtr hKey
    );

    /// <summary>
    /// 打开与进程关联的访问令牌 (OpenProcessToken)
    /// </summary>
    [DllImport(ADVAPI32_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out SafeAccessTokenHandle TokenHandle
    );

    /// <summary>
    /// 检索有关访问令牌的指定类型的信息 (GetTokenInformation)
    /// </summary>
    [DllImport(ADVAPI32_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetTokenInformation(
        SafeAccessTokenHandle TokenHandle,
        int TokenInformationClass,
        IntPtr TokenInformation,
        uint TokenInformationLength,
        out uint ReturnLength
    );

    /// <summary>
    /// 检索当前进程的伪句柄 (GetCurrentProcess)
    /// </summary>
    [DllImport(KERNEL32_LIB)]
    public static extern IntPtr GetCurrentProcess();

    /// <summary>
    /// 判断当前进程是否以管理员（提升的）特权运行
    /// </summary>
    public static bool IsProcessElevated()
    {
        const uint TOKEN_QUERY = 0x0008;
        const int TokenElevation = 20;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out var tokenHandle))
        {
            return false;
        }

        using (tokenHandle)
        {
            int elevationSize = Marshal.SizeOf<TOKEN_ELEVATION>();
            IntPtr pElevation = Marshal.AllocHGlobal(elevationSize);
            try
            {
                if (GetTokenInformation(tokenHandle, TokenElevation, pElevation, (uint)elevationSize, out _))
                {
                    var elevation = Marshal.PtrToStructure<TOKEN_ELEVATION>(pElevation);
                    return elevation.TokenIsElevated != 0;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pElevation);
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION
    {
        public int TokenIsElevated;
    }
}

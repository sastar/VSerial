using System;

namespace VSerial.Interop;

/// <summary>
/// Windows 原生底层常量与设备类 GUID 定义
/// </summary>
public static class NativeConstants
{
    /// <summary>
    /// 虚拟串口驱动硬件 ID
    /// </summary>
    public const string VSERIAL_HARDWARE_ID = @"UMDF\VirtualSerial2um";

    /// <summary>
    /// 串口设备安装类 GUID (Ports Class: {4d36e978-e325-11ce-bfc1-08002be10318})
    /// </summary>
    public static readonly Guid GUID_DEVCLASS_PORTS = new("4d36e978-e325-11ce-bfc1-08002be10318");

    /// <summary>
    /// 串口设备接口 GUID (GUID_DEVINTERFACE_COMPORT: {86e0d1e0-8089-11d0-9ce4-08003e301f73})
    /// </summary>
    public static readonly Guid GUID_DEVINTERFACE_COMPORT = new("86e0d1e0-8089-11d0-9ce4-08003e301f73");

    // SetupDiCreateDeviceInfo DICD 标志位
    public const uint DICD_GENERATE_ID = 0x00000001;

    // SetupDiGet/SetDeviceRegistryProperty 属性代码
    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    // 设备安装函数 (DIF) 消息代码
    public const uint DIF_INSTALLDEVICE = 0x00000001;
    public const uint DIF_REMOVE = 0x00000005;
    public const uint DIF_REGISTERDEVICE = 0x00000019;

    // 设备注册表键标识 (DICS_FLAG & DIREG)
    public const uint DICS_FLAG_GLOBAL = 0x00000001;
    public const uint DIREG_DEV = 0x00000001;

    // SetupDiBuildDriverInfoList 驱动程序类型
    public const uint SPDIT_COMPATDRIVER = 0x00000002;

    // 设备安装控制参数标志 (SP_DEVINSTALL_PARAMS.Flags)
    public const uint DI_NOBROWSE = 0x00000020;
    public const uint DI_QUIETINSTALL = 0x00000080;

    // SetupDiGetClassDevs 控制标志
    public const uint DIGCF_DEFAULT = 0x00000001;
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_ALLCLASSES = 0x00000004;
    public const uint DIGCF_PROFILE = 0x00000008;
    public const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    // 注册表访问权限控制标志
    public const uint KEY_QUERY_VALUE = 0x0001;
    public const uint KEY_SET_VALUE = 0x0002;
    public const uint KEY_READ = 0x20019;
    public const uint KEY_ALL_ACCESS = 0xF003F;

    // 注册表值数据类型
    public const uint REG_SZ = 1;
    public const uint REG_BINARY = 3;
    public const uint REG_DWORD = 4;
    public const uint REG_MULTI_SZ = 7;

    // CfgMgr32 配置管理器相关返回值与标志位
    public const uint CR_SUCCESS = 0x00000000;
    public const uint CM_REENUMERATE_NORMAL = 0x00000000;
    public const uint DN_STARTED = 0x00000008;
    public const int MAX_DEVICE_ID_LEN = 200;

    // NewDev 设备安装与驱动卸载相关标志位
    public const uint INSTALLFLAG_FORCE = 0x00000001;
    public const uint INSTALLFLAG_READONLY = 0x00000002;
    public const uint SPOST_PATH = 0x00000001;

    // 常用 Win32 错误码
    public const uint ERROR_SUCCESS = 0;
    public const uint ERROR_ACCESS_DENIED = 5;
    public const uint ERROR_NO_MORE_ITEMS = 259;
    public const uint ERROR_DI_DO_DEFAULT = 0xE000020E;
    public const uint CERT_E_UNTRUSTEDROOT = 0x800B0109;

    // Windows 加密服务体系 (CryptoAPI) 常量
    public const uint X509_ASN_ENCODING = 0x00000001;
    public const uint PKCS_7_ASN_ENCODING = 0x00010000;
    public const uint CERT_SYSTEM_STORE_LOCAL_MACHINE = 0x00020000;
    public const uint CERT_STORE_ADD_REPLACE_EXISTING = 3;
    public const uint CERT_STORE_PROV_SYSTEM_W = 10;
}

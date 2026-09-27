namespace VSerial.Core.Exceptions;

/// <summary>
/// 虚拟串口系统业务错误代码枚举
/// </summary>
public enum VSerialErrorCode
{
    /// <summary>
    /// 操作成功
    /// </summary>
    Success = 0,

    /// <summary>
    /// 请求的端口已被占用（无论是物理串口或其他虚拟串口）
    /// </summary>
    PortAlreadyInUse = 1001,

    /// <summary>
    /// 配对组 ID (Pair ID) 已经存在，不能重复添加
    /// </summary>
    PairIdAlreadyExists = 1002,

    /// <summary>
    /// 未找到指定的虚拟串口对
    /// </summary>
    PairNotFound = 1003,

    /// <summary>
    /// 端口名称格式无效（必须符合 COM[1-256] 规范）
    /// </summary>
    InvalidPortName = 1004,

    /// <summary>
    /// 成对的两个端口名称不能相同
    /// </summary>
    PortNamesMustDiffer = 1005,

    /// <summary>
    /// 驱动程序尚未安装到系统 DriverStore 中
    /// </summary>
    DriverNotInstalled = 2001,

    /// <summary>
    /// 驱动测试签名证书未受信任，需要在 ROOT 与 TrustedPublisher 导入
    /// </summary>
    CertificateNotTrusted = 2002,

    /// <summary>
    /// 驱动 INF 描述文件不存在或无法访问
    /// </summary>
    InfFileNotFound = 2003,

    /// <summary>
    /// 驱动卸载失败
    /// </summary>
    DriverUninstallFailed = 2004,

    /// <summary>
    /// 权限不足（修改硬件设备必须以管理员权限运行）
    /// </summary>
    AccessDenied = 3001,

    /// <summary>
    /// 创建 B 端失败，已自动回滚清理 A 端端口
    /// </summary>
    DeviceCreationRollback = 4001,

    /// <summary>
    /// 底层 SetupAPI / CfgMgr 原生调用返回错误
    /// </summary>
    NativeSetupApiError = 5001,

    /// <summary>
    /// 未知内部错误
    /// </summary>
    Unknown = 9999
}

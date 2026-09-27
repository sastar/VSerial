namespace VSerial.Core.Models;

/// <summary>
/// 虚拟串口配对端口角色
/// </summary>
public enum PortRole
{
    /// <summary>
    /// 配对的 A 端端口 (角色码 0)
    /// </summary>
    RoleA = 0,

    /// <summary>
    /// 配对的 B 端端口 (角色码 1)
    /// </summary>
    RoleB = 1
}

/// <summary>
/// 虚拟串口对运行状态枚举
/// </summary>
public enum PairStatus
{
    /// <summary>
    /// 正常运行：A、B 两端均已成功启动 (DN_STARTED)
    /// </summary>
    Normal,

    /// <summary>
    /// 存在设备故障：某端或两端驱动出现错误代码 (ProblemCode > 0)
    /// </summary>
    DeviceProblem,

    /// <summary>
    /// 配对残缺：仅存在单端设备，另一端缺失
    /// </summary>
    Incomplete
}

/// <summary>
/// 虚拟串口驱动运行状态
/// </summary>
public enum DriverOperationalStatus
{
    /// <summary>
    /// 驱动尚未预安装到系统驱动存储区 (DriverStore)
    /// </summary>
    NotInstalled,

    /// <summary>
    /// 驱动已安装但当前未激活或没有相关设备在运行
    /// </summary>
    InstalledIdle,

    /// <summary>
    /// 驱动已成功加载并正在为虚拟串口对提供数据中继
    /// </summary>
    Running
}

/// <summary>
/// 统一日志等级枚举
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

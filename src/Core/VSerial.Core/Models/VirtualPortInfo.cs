namespace VSerial.Core.Models;

/// <summary>
/// 单个虚拟串口设备节点详细信息模型
/// </summary>
public class VirtualPortInfo
{
    /// <summary>
    /// 端口名称 (例如 COM10)
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// 所属配对组 ID
    /// </summary>
    public int PairId { get; set; }

    /// <summary>
    /// 端口角色 (RoleA 或 RoleB)
    /// </summary>
    public PortRole Role { get; set; } = PortRole.RoleA;

    /// <summary>
    /// Windows PnP 设备实例 ID (Device Instance ID)
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 设备是否已经处于正常运行启动状态 (DN_STARTED)
    /// </summary>
    public bool IsStarted { get; set; }

    /// <summary>
    /// Windows 设备管理器问题代码 (若正常运行则为 0)
    /// </summary>
    public uint ProblemCode { get; set; }

    /// <summary>
    /// 人性化的端口运行状态中文描述
    /// </summary>
    public string StatusDescription
    {
        get
        {
            if (IsStarted && ProblemCode == 0)
            {
                return "正常运行";
            }

            return ProblemCode switch
            {
                1 => "设备未正确配置 (Code 1)",
                10 => "设备无法启动 (Code 10: 驱动可能未签名或加载失败)",
                18 => "请为此设备重新安装驱动程序 (Code 18)",
                28 => "未安装驱动程序 (Code 28)",
                31 => "驱动程序无法正常工作 (Code 31)",
                39 => "Windows 无法加载驱动程序 (Code 39: 驱动文件可能丢失或签名无效)",
                52 => "驱动程序数字签名无效或未受信任 (Code 52)",
                _ => $"设备异常 (代码 {ProblemCode})"
            };
        }
    }

    public override string ToString()
    {
        return $"{PortName} (Pair {PairId}, Role {Role}) - {StatusDescription}";
    }
}

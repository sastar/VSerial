namespace VSerial.Core.Models;

/// <summary>
/// 成对虚拟串口模型 (例如 COM10 ↔ COM11)
/// </summary>
public class VirtualPortPair
{
    /// <summary>
    /// 配对组唯一标识 ID
    /// </summary>
    public int PairId { get; set; }

    /// <summary>
    /// 配对中的 A 端串口信息 (Role 0)
    /// </summary>
    public VirtualPortInfo PortA { get; set; } = new();

    /// <summary>
    /// 配对中的 B 端串口信息 (Role 1)
    /// </summary>
    public VirtualPortInfo PortB { get; set; } = new();

    /// <summary>
    /// 是否存在 A 端设备节点
    /// </summary>
    public bool HasPortA { get; set; }

    /// <summary>
    /// 是否存在 B 端设备节点
    /// </summary>
    public bool HasPortB { get; set; }

    /// <summary>
    /// 配对综合运行状态
    /// </summary>
    public PairStatus Status
    {
        get
        {
            if (HasPortA && HasPortB)
            {
                if (PortA.IsStarted && PortB.IsStarted && PortA.ProblemCode == 0 && PortB.ProblemCode == 0)
                {
                    return PairStatus.Normal;
                }

                return PairStatus.DeviceProblem;
            }

            return PairStatus.Incomplete;
        }
    }

    /// <summary>
    /// 配对端口可读显示文本 (例如 "COM10 <-> COM11")
    /// </summary>
    public string DisplayName
    {
        get
        {
            string a = HasPortA ? PortA.PortName : "[未分配]";
            string b = HasPortB ? PortB.PortName : "[未分配]";
            return $"{a} <-> {b}";
        }
    }

    /// <summary>
    /// 状态的中文详细描述
    /// </summary>
    public string StatusDescription
    {
        get
        {
            return Status switch
            {
                PairStatus.Normal => "正常互联 (Started OK)",
                PairStatus.DeviceProblem => $"端口异常: A端[{PortA.StatusDescription}], B端[{PortB.StatusDescription}]",
                PairStatus.Incomplete => HasPortA ? "配对缺失 B 端端口" : "配对缺失 A 端端口",
                _ => "未知状态"
            };
        }
    }

    public override string ToString()
    {
        return $"[Pair {PairId}] {DisplayName} - {StatusDescription}";
    }
}

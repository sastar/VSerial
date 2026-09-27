namespace VSerial.Core.Models;

/// <summary>
/// 虚拟串口驱动运行与部署状态模型
/// </summary>
public class DriverStatusInfo
{
    /// <summary>
    /// 驱动程序是否已成功预安装到 DriverStore
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// 当前驱动运行状态
    /// </summary>
    public DriverOperationalStatus OperationalStatus { get; set; } = DriverOperationalStatus.NotInstalled;

    /// <summary>
    /// 驱动描述文件 INF 的解析路径 (如果存在)
    /// </summary>
    public string InfPath { get; set; } = string.Empty;

    /// <summary>
    /// 系统当前已安装的虚拟串口对总数
    /// </summary>
    public int ActivePairsCount { get; set; }

    /// <summary>
    /// 驱动状态简要描述
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;
}

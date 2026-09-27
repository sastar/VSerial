using System.Threading;
using System.Threading.Tasks;
using VSerial.Core.Models;

namespace VSerial.Core.Services;

/// <summary>
/// 虚拟串口底层驱动程序包管理与生命周期服务接口
/// </summary>
public interface IDriverService
{
    /// <summary>
    /// 查询当前虚拟串口驱动程序在系统中的安装与运行状态
    /// </summary>
    Task<DriverStatusInfo> GetDriverStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 将驱动程序包预安装并注册到 Windows DriverStore，并自动配置测试证书信任
    /// </summary>
    /// <param name="customInfPath">可选指定的 INF 描述文件路径 (为空时自动探测)</param>
    /// <param name="cancellationToken">取消标记</param>
    Task InstallDriverAsync(string? customInfPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从 Windows DriverStore 彻底卸载驱动程序并级联清理所有关联的虚拟串口设备节点
    /// </summary>
    /// <param name="customInfPath">可选指定的 INF 描述文件路径 (为空时自动探测)</param>
    /// <param name="cancellationToken">取消标记</param>
    Task UninstallDriverAsync(string? customInfPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 自动探测系统中默认的 virtualserial2um.inf 文件绝对路径
    /// </summary>
    string? FindDefaultInfPath();
}

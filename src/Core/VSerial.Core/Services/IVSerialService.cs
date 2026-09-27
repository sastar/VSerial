using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VSerial.Core.Models;

namespace VSerial.Core.Services;

/// <summary>
/// 虚拟串口核心业务管理服务接口
/// </summary>
public interface IVSerialService
{
    /// <summary>
    /// 获取当前系统中所有虚拟串口对列表
    /// </summary>
    Task<IReadOnlyList<VirtualPortPair>> GetPairsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 PairId 获取指定的虚拟串口对
    /// </summary>
    Task<VirtualPortPair?> GetPairByIdAsync(int pairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建一对互联虚拟串口 (包含端口格式校验、重复防冲突检测、原子回滚机制)
    /// </summary>
    /// <param name="pairId">配对唯一组 ID</param>
    /// <param name="portA">A 端端口名称 (例如 COM10)</param>
    /// <param name="portB">B 端端口名称 (例如 COM11)</param>
    /// <param name="cancellationToken">取消标记</param>
    /// <returns>创建成功的虚拟串口对模型</returns>
    Task<VirtualPortPair> CreatePairAsync(int pairId, string portA, string portB, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定 PairId 的虚拟串口对
    /// </summary>
    Task DeletePairAsync(int pairId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除系统中所有已安装的虚拟串口对
    /// </summary>
    Task DeleteAllPairsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 智能分析当前系统环境，推荐下一个可用的 PairId 及未被占用的成对端口号
    /// </summary>
    Task<(int nextPairId, string suggestedPortA, string suggestedPortB)> SuggestNextPairAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前系统中所有已被占用的 COM 端口号集合 (包括物理串口与所有虚拟串口)
    /// </summary>
    Task<ISet<string>> GetOccupiedPortsAsync(CancellationToken cancellationToken = default);
}

using VSerial.Core.Models;
using Xunit;

namespace VSerial.Core.Tests.Models;

/// <summary>
/// 虚拟串口配对模型综合状态与展示单元测试
/// </summary>
public class VirtualPortPairTests
{
    [Fact]
    public void Status_WhenBothPortsStartedWithoutProblems_ShouldBeNormal()
    {
        // 构造完整的正常运行配对
        var pair = new VirtualPortPair
        {
            PairId = 1,
            HasPortA = true,
            HasPortB = true,
            PortA = new VirtualPortInfo { PortName = "COM10", IsStarted = true, ProblemCode = 0 },
            PortB = new VirtualPortInfo { PortName = "COM11", IsStarted = true, ProblemCode = 0 }
        };

        // 验证状态与描述
        Assert.Equal(PairStatus.Normal, pair.Status);
        Assert.Equal("COM10 <-> COM11", pair.DisplayName);
        Assert.Equal("正常互联 (Started OK)", pair.StatusDescription);
        Assert.Contains("[Pair 1] COM10 <-> COM11 - 正常互联 (Started OK)", pair.ToString());
    }

    [Fact]
    public void Status_WhenPortBMissing_ShouldBeIncomplete()
    {
        // 构造缺失 B 端的配对
        var pair = new VirtualPortPair
        {
            PairId = 2,
            HasPortA = true,
            HasPortB = false,
            PortA = new VirtualPortInfo { PortName = "COM12", IsStarted = true, ProblemCode = 0 }
        };

        // 验证状态与描述
        Assert.Equal(PairStatus.Incomplete, pair.Status);
        Assert.Equal("COM12 <-> [未分配]", pair.DisplayName);
        Assert.Equal("配对缺失 B 端端口", pair.StatusDescription);
    }

    [Fact]
    public void Status_WhenPortAMissing_ShouldBeIncomplete()
    {
        // 构造缺失 A 端的配对
        var pair = new VirtualPortPair
        {
            PairId = 3,
            HasPortA = false,
            HasPortB = true,
            PortB = new VirtualPortInfo { PortName = "COM13", IsStarted = true, ProblemCode = 0 }
        };

        // 验证状态与描述
        Assert.Equal(PairStatus.Incomplete, pair.Status);
        Assert.Equal("[未分配] <-> COM13", pair.DisplayName);
        Assert.Equal("配对缺失 A 端端口", pair.StatusDescription);
    }

    [Fact]
    public void Status_WhenBothPortsPresentButHasDeviceProblem_ShouldBeDeviceProblem()
    {
        // 构造带有设备驱动故障的配对
        var pair = new VirtualPortPair
        {
            PairId = 4,
            HasPortA = true,
            HasPortB = true,
            PortA = new VirtualPortInfo { PortName = "COM14", IsStarted = true, ProblemCode = 0 },
            PortB = new VirtualPortInfo { PortName = "COM15", IsStarted = false, ProblemCode = 28 } // Code 28: 未安装驱动
        };

        // 验证状态与描述
        Assert.Equal(PairStatus.DeviceProblem, pair.Status);
        Assert.Equal("COM14 <-> COM15", pair.DisplayName);
        Assert.Contains("端口异常", pair.StatusDescription);
        Assert.Contains("未安装驱动程序", pair.StatusDescription);
    }
}

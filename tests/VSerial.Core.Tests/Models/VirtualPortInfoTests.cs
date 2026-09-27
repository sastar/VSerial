using VSerial.Core.Models;
using Xunit;

namespace VSerial.Core.Tests.Models;

/// <summary>
/// 虚拟串口单端口模型与状态说明单元测试
/// </summary>
public class VirtualPortInfoTests
{
    [Fact]
    public void StatusDescription_WhenStartedAndNoProblem_ShouldReturnNormal()
    {
        // 构造正常启动的端口对象
        var port = new VirtualPortInfo
        {
            PortName = "COM10",
            PairId = 1,
            Role = PortRole.RoleA,
            IsStarted = true,
            ProblemCode = 0
        };

        // 验证状态描述
        Assert.Equal("正常运行", port.StatusDescription);
        Assert.Contains("COM10", port.ToString());
        Assert.Contains("正常运行", port.ToString());
    }

    [Theory]
    [InlineData(1u, "设备未正确配置 (Code 1)")]
    [InlineData(10u, "设备无法启动 (Code 10: 驱动可能未签名或加载失败)")]
    [InlineData(18u, "请为此设备重新安装驱动程序 (Code 18)")]
    [InlineData(28u, "未安装驱动程序 (Code 28)")]
    [InlineData(31u, "驱动程序无法正常工作 (Code 31)")]
    [InlineData(39u, "Windows 无法加载驱动程序 (Code 39: 驱动文件可能丢失或签名无效)")]
    [InlineData(52u, "驱动程序数字签名无效或未受信任 (Code 52)")]
    [InlineData(99u, "设备异常 (代码 99)")]
    public void StatusDescription_WithVariousProblemCodes_ShouldReturnFriendlyChineseDescription(uint problemCode, string expectedDescription)
    {
        // 构造带有故障代码的端口对象
        var port = new VirtualPortInfo
        {
            PortName = "COM11",
            PairId = 1,
            Role = PortRole.RoleB,
            IsStarted = false,
            ProblemCode = problemCode
        };

        // 验证状态描述
        Assert.Equal(expectedDescription, port.StatusDescription);
        Assert.Contains(expectedDescription, port.ToString());
    }
}

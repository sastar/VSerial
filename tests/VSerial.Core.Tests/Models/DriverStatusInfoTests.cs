using VSerial.Core.Models;
using Xunit;

namespace VSerial.Core.Tests.Models;

/// <summary>
/// 驱动状态信息模型单元测试
/// </summary>
public class DriverStatusInfoTests
{
    [Fact]
    public void DriverStatusInfo_DefaultValues_ShouldBeConsistent()
    {
        // 实例化驱动状态对象
        var info = new DriverStatusInfo();

        // 验证默认初值
        Assert.False(info.IsInstalled);
        Assert.Equal(DriverOperationalStatus.NotInstalled, info.OperationalStatus);
        Assert.Equal(string.Empty, info.InfPath);
        Assert.Equal(0, info.ActivePairsCount);
        Assert.Equal(string.Empty, info.StatusMessage);
    }

    [Fact]
    public void DriverStatusInfo_CustomValues_ShouldStoreCorrectly()
    {
        // 实例化并赋值
        var info = new DriverStatusInfo
        {
            IsInstalled = true,
            OperationalStatus = DriverOperationalStatus.Running,
            InfPath = @"C:\Drivers\virtualserial2um.inf",
            ActivePairsCount = 3,
            StatusMessage = "驱动已就绪"
        };

        // 验证属性赋值结果
        Assert.True(info.IsInstalled);
        Assert.Equal(DriverOperationalStatus.Running, info.OperationalStatus);
        Assert.Equal(@"C:\Drivers\virtualserial2um.inf", info.InfPath);
        Assert.Equal(3, info.ActivePairsCount);
        Assert.Equal("驱动已就绪", info.StatusMessage);
    }
}

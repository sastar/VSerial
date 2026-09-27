using System;
using System.Collections.Generic;
using VSerial.Core.Services;
using Xunit;

namespace VSerial.Core.Tests.Services;

/// <summary>
/// 系统串口检测服务单元测试
/// </summary>
public class SystemPortDetectorTests
{
    [Fact]
    public void GetSystemActivePorts_ShouldReturnNonNullSetWithoutThrowing()
    {
        // 执行系统活跃端口查询
        HashSet<string> activePorts = SystemPortDetector.GetSystemActivePorts();

        // 验证返回结果集合非空指针
        Assert.NotNull(activePorts);

        // 如果系统当前已配置有 COM10，验证其规范化形式包含在内
        // 注意：即使在全新机器上，集合也可能为空，但绝不能抛出异常
        foreach (var port in activePorts)
        {
            Assert.False(string.IsNullOrWhiteSpace(port));
            Assert.StartsWith("COM", port, StringComparison.OrdinalIgnoreCase);
        }
    }
}

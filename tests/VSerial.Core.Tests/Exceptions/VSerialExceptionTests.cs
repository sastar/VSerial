using System;
using VSerial.Core.Exceptions;
using Xunit;

namespace VSerial.Core.Tests.Exceptions;

/// <summary>
/// 虚拟串口异常体系与错误码单元测试
/// </summary>
public class VSerialExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetPropertiesAndFormatMessageCorrectly()
    {
        // 构造自定义异常
        var inner = new InvalidOperationException("内部原生异常");
        var ex = new VSerialException(
            VSerialErrorCode.NativeSetupApiError,
            "调用 SetupDiSetDeviceRegistryPropertyW 失败",
            "设置硬件ID",
            13, // ERROR_INVALID_DATA
            inner
        );

        // 验证属性赋值
        Assert.Equal(VSerialErrorCode.NativeSetupApiError, ex.ErrorCode);
        Assert.Equal("设置硬件ID", ex.Operation);
        Assert.Equal(13u, ex.NativeErrorCode);
        Assert.Same(inner, ex.InnerException);

        // 验证格式化字符串
        Assert.Contains("[设置硬件ID]", ex.Message);
        Assert.Contains("调用 SetupDiSetDeviceRegistryPropertyW 失败", ex.Message);
        Assert.Contains("底层 Win32 错误码: 13", ex.Message);
    }

    [Fact]
    public void CreateAccessDenied_ShouldCreateStandardException()
    {
        // 调用便捷工厂方法
        var ex = VSerialException.CreateAccessDenied("安装驱动证书");

        // 验证错误码与 NativeErrorCode 为 5
        Assert.Equal(VSerialErrorCode.AccessDenied, ex.ErrorCode);
        Assert.Equal("安装驱动证书", ex.Operation);
        Assert.Equal(5u, ex.NativeErrorCode);
        Assert.Contains("权限不足", ex.Message);
        Assert.Contains("以管理员身份运行", ex.Message);
    }

    [Fact]
    public void CreatePortConflict_ShouldCreateConflictExceptionWithPortName()
    {
        // 调用冲突便捷工厂方法
        var ex = VSerialException.CreatePortConflict("COM10", "创建串口对");

        // 验证错误码与提示包含端口名称
        Assert.Equal(VSerialErrorCode.PortAlreadyInUse, ex.ErrorCode);
        Assert.Equal("创建串口对", ex.Operation);
        Assert.Contains("COM10", ex.Message);
        Assert.Contains("端口冲突", ex.Message);
    }
}

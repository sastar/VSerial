using System;
using System.Threading.Tasks;
using VSerial.Core.Exceptions;
using VSerial.Core.Models;
using VSerial.Core.Services;
using Xunit;

namespace VSerial.Core.Tests.Services;

/// <summary>
/// 驱动管理服务状态查询与权限验证单元测试
/// </summary>
public class DriverServiceTests
{
    private readonly IVSerialService _vserialService = new VSerialService();
    private readonly IDriverService _driverService;

    public DriverServiceTests()
    {
        _driverService = new DriverService(_vserialService);
    }

    [Fact]
    public async Task GetDriverStatusAsync_ShouldReturnNonNullStatus()
    {
        // 查询驱动状态
        var status = await _driverService.GetDriverStatusAsync();

        // 验证返回状态对象不为 null 且包含描述信息
        Assert.NotNull(status);
        Assert.False(string.IsNullOrWhiteSpace(status.StatusMessage));
        Assert.True(status.ActivePairsCount >= 0);
    }

    [Fact]
    public async Task InstallDriverAsync_WithoutAdmin_ShouldThrowAccessDenied()
    {
        // 如果当前未以管理员提权运行，应拦截并抛出 AccessDenied
        if (!VSerial.Interop.NativeMethods.IsProcessElevated())
        {
            var ex = await Assert.ThrowsAsync<VSerialException>(() =>
                _driverService.InstallDriverAsync());

            Assert.Equal(VSerialErrorCode.AccessDenied, ex.ErrorCode);
            Assert.Contains("权限不足", ex.Message);
        }
    }

    [Fact]
    public async Task UninstallDriverAsync_WithoutAdmin_ShouldThrowAccessDenied()
    {
        // 如果当前未以管理员提权运行，应拦截并抛出 AccessDenied
        if (!VSerial.Interop.NativeMethods.IsProcessElevated())
        {
            var ex = await Assert.ThrowsAsync<VSerialException>(() =>
                _driverService.UninstallDriverAsync());

            Assert.Equal(VSerialErrorCode.AccessDenied, ex.ErrorCode);
            Assert.Contains("权限不足", ex.Message);
        }
    }
}

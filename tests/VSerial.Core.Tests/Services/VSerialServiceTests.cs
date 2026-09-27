using System;
using System.Threading.Tasks;
using VSerial.Core.Exceptions;
using VSerial.Core.Models;
using VSerial.Core.Services;
using Xunit;

namespace VSerial.Core.Tests.Services;

/// <summary>
/// 虚拟串口服务参数校验与基础行为单元测试
/// </summary>
public class VSerialServiceTests
{
    private readonly IVSerialService _service = new VSerialService();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task CreatePairAsync_InvalidPairId_ShouldThrowInvalidPortName(int invalidPairId)
    {
        // 验证非法配对 ID 抛出异常
        var ex = await Assert.ThrowsAsync<VSerialException>(() =>
            _service.CreatePairAsync(invalidPairId, "COM10", "COM11"));

        Assert.Equal(VSerialErrorCode.InvalidPortName, ex.ErrorCode);
        Assert.Contains("大于 0 的正整数", ex.Message);
    }

    [Theory]
    [InlineData("INVALID", "COM11")]
    [InlineData("COM0", "COM11")]
    [InlineData("COM257", "COM11")]
    [InlineData("COM10", "COM")]
    [InlineData("COM10", "LPT1")]
    [InlineData("", "COM11")]
    public async Task CreatePairAsync_InvalidPortNames_ShouldThrowInvalidPortName(string portA, string portB)
    {
        // 验证非法端口命名抛出异常
        var ex = await Assert.ThrowsAsync<VSerialException>(() =>
            _service.CreatePairAsync(1, portA, portB));

        Assert.Equal(VSerialErrorCode.InvalidPortName, ex.ErrorCode);
        Assert.Contains("端口格式不合法", ex.Message);
    }

    [Theory]
    [InlineData("COM10", "COM10")]
    [InlineData("com10", "COM10")]
    [InlineData(" COM20 ", "com20")]
    public async Task CreatePairAsync_IdenticalPortNames_ShouldThrowPortNamesMustDiffer(string portA, string portB)
    {
        // 验证相同端口成对创建抛出异常
        var ex = await Assert.ThrowsAsync<VSerialException>(() =>
            _service.CreatePairAsync(1, portA, portB));

        Assert.Equal(VSerialErrorCode.PortNamesMustDiffer, ex.ErrorCode);
        Assert.Contains("成对的端口名称不能相同", ex.Message);
    }

    [Fact]
    public async Task GetPairsAsync_ShouldReturnReadOnlyListWithoutException()
    {
        // 枚举已存在的虚拟串口
        var pairs = await _service.GetPairsAsync();

        // 验证返回结果不为 null
        Assert.NotNull(pairs);

        // 如果存在配对，验证其结构合法
        foreach (var pair in pairs)
        {
            Assert.True(pair.PairId > 0);
            Assert.False(string.IsNullOrWhiteSpace(pair.DisplayName));
        }
    }

    [Fact]
    public async Task SuggestNextPairAsync_ShouldReturnValidRecommendations()
    {
        // 执行自动推荐计算
        var (nextId, suggestedPortA, suggestedPortB) = await _service.SuggestNextPairAsync();

        // 验证推荐结果合法性
        Assert.True(nextId >= 1);
        Assert.True(SystemPortDetector.IsValidPortName(suggestedPortA, out int portNumA));
        Assert.True(SystemPortDetector.IsValidPortName(suggestedPortB, out int portNumB));
        Assert.NotEqual(suggestedPortA, suggestedPortB, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(portNumA + 1, portNumB);
    }

    [Fact]
    public async Task GetOccupiedPortsAsync_ShouldReturnValidSet()
    {
        // 获取占用集合
        var occupied = await _service.GetOccupiedPortsAsync();

        // 验证非空
        Assert.NotNull(occupied);
    }
}

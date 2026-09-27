using System;
using VSerial.Core.Services;
using Xunit;

namespace VSerial.Core.Tests.Validation;

/// <summary>
/// COM 端口名称校验与格式规范化单元测试
/// </summary>
public class PortNameValidationTests
{
    [Theory]
    [InlineData("COM1", 1)]
    [InlineData("COM2", 2)]
    [InlineData("COM9", 9)]
    [InlineData("COM10", 10)]
    [InlineData("COM99", 99)]
    [InlineData("COM100", 100)]
    [InlineData("COM255", 255)]
    [InlineData("COM256", 256)]
    public void IsValidPortName_ValidStandardPorts_ShouldReturnTrue(string portName, int expectedNumber)
    {
        // 执行校验
        bool isValid = SystemPortDetector.IsValidPortName(portName, out int actualNumber);

        // 断言验证
        Assert.True(isValid);
        Assert.Equal(expectedNumber, actualNumber);
    }

    [Theory]
    [InlineData("com1", 1)]
    [InlineData("com10", 10)]
    [InlineData("cOm256", 256)]
    [InlineData(" COM10 ", 10)]
    public void IsValidPortName_CaseInsensitiveAndTrimming_ShouldReturnTrue(string portName, int expectedNumber)
    {
        // 执行校验
        bool isValid = SystemPortDetector.IsValidPortName(portName, out int actualNumber);

        // 断言验证
        Assert.True(isValid);
        Assert.Equal(expectedNumber, actualNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("COM0")]
    [InlineData("COM257")]
    [InlineData("COM1000")]
    [InlineData("COM-1")]
    [InlineData("COM")]
    [InlineData("COM01")]
    [InlineData("LPT1")]
    [InlineData("SERIAL1")]
    [InlineData("COM1A")]
    [InlineData("COM 1")]
    [InlineData("VCOM10")]
    public void IsValidPortName_InvalidOrOutOfRangePorts_ShouldReturnFalse(string? portName)
    {
        // 执行校验
        bool isValid = SystemPortDetector.IsValidPortName(portName!, out int actualNumber);

        // 断言验证
        Assert.False(isValid);
        Assert.Equal(0, actualNumber);
    }

    [Theory]
    [InlineData("com1", "COM1")]
    [InlineData("com10", "COM10")]
    [InlineData("  com99  ", "COM99")]
    [InlineData("CoM256", "COM256")]
    public void NormalizePortName_ShouldTrimAndConvertToUpperCase(string input, string expected)
    {
        // 执行规范化
        string result = SystemPortDetector.NormalizePortName(input);

        // 断言验证
        Assert.Equal(expected, result);
    }
}

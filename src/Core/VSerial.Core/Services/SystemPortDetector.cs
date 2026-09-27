using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VSerial.Core.Services;

/// <summary>
/// 系统串口检测与合法性校验工具
/// </summary>
public static class SystemPortDetector
{
    private static readonly Regex PortNameRegex = new(@"^COM([1-9][0-9]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 校验端口名称格式是否符合标准 (例如 "COM1" 到 "COM256")
    /// </summary>
    public static bool IsValidPortName(string portName, out int portNumber)
    {
        portNumber = 0;
        if (string.IsNullOrWhiteSpace(portName))
        {
            return false;
        }

        var match = PortNameRegex.Match(portName.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (int.TryParse(match.Groups[1].Value, out int num) && num > 0 && num <= 256)
        {
            portNumber = num;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 规范化端口名称 (转换为全大写标准形式，例如 "com10" -> "COM10")
    /// </summary>
    public static string NormalizePortName(string portName)
    {
        return portName.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// 从注册表 HARDWARE\DEVICEMAP\SERIALCOMM 获取系统当前登记的所有活跃串口
    /// </summary>
    public static HashSet<string> GetSystemActivePorts()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM", false);
            if (key != null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    var val = key.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        ports.Add(NormalizePortName(val));
                    }
                }
            }
        }
        catch
        {
            // 忽略注册表只读查询异常
        }

        return ports;
    }
}

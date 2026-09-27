using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VSerial.Core.Models;
using VSerial.UI.Localization;

namespace VSerial.UI.Converters;

/// <summary>
/// 虚拟串口配对状态转换为指示灯/徽章画刷颜色
/// </summary>
public class PairStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush NormalBrush = new((Color)ColorConverter.ConvertFromString("#10B981")); // 翡翠绿
    private static readonly SolidColorBrush ProblemBrush = new((Color)ColorConverter.ConvertFromString("#EF4444")); // 亮红
    private static readonly SolidColorBrush IncompleteBrush = new((Color)ColorConverter.ConvertFromString("#F59E0B")); // 琥珀黄
    private static readonly SolidColorBrush UnknownBrush = new((Color)ColorConverter.ConvertFromString("#6B7280")); // 中性灰

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PairStatus status)
        {
            return status switch
            {
                PairStatus.Normal => NormalBrush,
                PairStatus.DeviceProblem => ProblemBrush,
                PairStatus.Incomplete => IncompleteBrush,
                _ => UnknownBrush
            };
        }

        if (value is bool isStarted)
        {
            return isStarted ? NormalBrush : ProblemBrush;
        }

        return UnknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 配对状态转换为简明徽章标签文字
/// </summary>
public class PairStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PairStatus status)
        {
            return status switch
            {
                PairStatus.Normal => LocalizationManager.GetString("Status_Normal", "Ready"),
                PairStatus.DeviceProblem => LocalizationManager.GetString("Status_DeviceProblem", "Port Fault"),
                PairStatus.Incomplete => LocalizationManager.GetString("Status_Incomplete", "Incomplete"),
                _ => LocalizationManager.GetString("Status_Unknown", "Unknown")
            };
        }

        return LocalizationManager.GetString("Status_Unknown", "Unknown");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 单个端口运行状态转为本地化描述（例如 "(Running OK)" 或 "(正常运行)"）
/// </summary>
public class PortStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VirtualPortInfo port)
        {
            if (port.IsStarted && port.ProblemCode == 0)
            {
                return $"({LocalizationManager.GetString("Port_Running", "Running OK")})";
            }

            if (port.ProblemCode != 0)
            {
                return $"({LocalizationManager.Format("Port_ProblemCode", port.ProblemCode)})";
            }

            return $"({LocalizationManager.GetString("Port_Stopped", "Stopped")})";
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 驱动状态转为本地化详细描述
/// </summary>
public class DriverStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DriverStatusInfo driver)
        {
            if (driver.OperationalStatus == DriverOperationalStatus.Running)
            {
                return LocalizationManager.Format("Driver_Running_Format", driver.ActivePairsCount);
            }

            if (driver.OperationalStatus == DriverOperationalStatus.InstalledIdle)
            {
                if (driver.ActivePairsCount > 0)
                {
                    return LocalizationManager.Format("Driver_InstalledIdle_Pairs_Format", driver.ActivePairsCount);
                }
                return LocalizationManager.GetString("Driver_InstalledIdle_NoPairs", "Driver ready. No virtual port pairs created");
            }

            if (driver.OperationalStatus == DriverOperationalStatus.NotInstalled)
            {
                return LocalizationManager.GetString("Driver_NotInstalled", "VirtualSerial2um driver is not installed");
            }

            if (!string.IsNullOrEmpty(driver.StatusMessage))
            {
                return driver.StatusMessage;
            }
        }

        return LocalizationManager.GetString("Driver_Detecting", "Detecting driver...");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

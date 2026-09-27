using System;

namespace VSerial.Core.Logging;

/// <summary>
/// 控制台标准日志输出实现类
/// </summary>
public class ConsoleLogger : IVSerialLogger
{
    private readonly object _lock = new();

    public void Debug(string message)
    {
#if DEBUG
        WriteLine(ConsoleColor.DarkGray, "[DBG]", message);
#endif
    }

    public void Info(string message)
    {
        WriteLine(ConsoleColor.Cyan, "[INF]", message);
    }

    public void Warning(string message)
    {
        WriteLine(ConsoleColor.Yellow, "[WRN]", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        string fullMsg = exception == null ? message : $"{message} (Reason: {exception.Message})";
        WriteLine(ConsoleColor.Red, "[ERR]", fullMsg);
    }

    private void WriteLine(ConsoleColor color, string prefix, string message)
    {
        lock (_lock)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.Write($"{prefix} ");
                Console.ForegroundColor = originalColor;
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }
}

/// <summary>
/// 空日志记录器（空对象模式）
/// </summary>
public class NullLogger : IVSerialLogger
{
    public static NullLogger Instance { get; } = new();

    public void Debug(string message) { }
    public void Info(string message) { }
    public void Warning(string message) { }
    public void Error(string message, Exception? exception = null) { }
}

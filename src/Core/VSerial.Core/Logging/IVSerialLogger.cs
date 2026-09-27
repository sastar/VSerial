using System;

namespace VSerial.Core.Logging;

/// <summary>
/// 虚拟串口系统统一抽象日志接口
/// 用于解耦底层操作与 UI / 控制台日志展示
/// </summary>
public interface IVSerialLogger
{
    /// <summary>
    /// 记录调试级别日志
    /// </summary>
    void Debug(string message);

    /// <summary>
    /// 记录普通信息日志
    /// </summary>
    void Info(string message);

    /// <summary>
    /// 记录警告级别日志
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// 记录错误级别日志
    /// </summary>
    void Error(string message, Exception? exception = null);
}

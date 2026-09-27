using System;

namespace VSerial.Core.Exceptions;

/// <summary>
/// 虚拟串口系统业务统一异常类
/// </summary>
public class VSerialException : Exception
{
    /// <summary>
    /// 业务错误码
    /// </summary>
    public VSerialErrorCode ErrorCode { get; }

    /// <summary>
    /// 触发异常的具体业务操作 (例如 "创建虚拟串口对 COM10 <-> COM11")
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// 底层 Win32 / Win32Exception 错误码 (若有)
    /// </summary>
    public uint NativeErrorCode { get; }

    public VSerialException(
        VSerialErrorCode errorCode,
        string message,
        string operation = "",
        uint nativeErrorCode = 0,
        Exception? innerException = null)
        : base(FormatMessage(errorCode, message, operation, nativeErrorCode), innerException)
    {
        ErrorCode = errorCode;
        Operation = operation;
        NativeErrorCode = nativeErrorCode;
    }

    private static string FormatMessage(VSerialErrorCode errorCode, string message, string operation, uint nativeErrorCode)
    {
        string opPrefix = string.IsNullOrWhiteSpace(operation) ? "" : $"[{operation}] ";
        string nativeSuffix = nativeErrorCode != 0 ? $" (底层 Win32 错误码: {nativeErrorCode})" : "";
        return $"{opPrefix}{message}{nativeSuffix}";
    }

    /// <summary>
    /// 便捷工厂方法：根据 Win32 权限拒绝生成友好异常
    /// </summary>
    public static VSerialException CreateAccessDenied(string operation)
    {
        return new VSerialException(
            VSerialErrorCode.AccessDenied,
            "权限不足：此操作需要修改 Windows 硬件设备，请右键选择“以管理员身份运行”。",
            operation,
            5 // ERROR_ACCESS_DENIED
        );
    }

    /// <summary>
    /// 便捷工厂方法：端口冲突
    /// </summary>
    public static VSerialException CreatePortConflict(string portName, string operation)
    {
        return new VSerialException(
            VSerialErrorCode.PortAlreadyInUse,
            $"端口冲突：串口 {portName} 已经被系统或现有虚拟串口占用，请指定其他未使用的 COM 端口号。",
            operation
        );
    }
}

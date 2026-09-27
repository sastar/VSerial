using System;
using System.Runtime.InteropServices;

namespace VSerial.Interop;

public static partial class NativeMethods
{
    private const string CRYPT32_LIB = "crypt32.dll";

    /// <summary>
    /// 从已编码的证书字节数组创建证书上下文 (CertCreateCertificateContext)
    /// </summary>
    [DllImport(CRYPT32_LIB, SetLastError = true)]
    public static extern SafeCertContextHandle CertCreateCertificateContext(
        uint dwCertEncodingType,
        byte[] pbCertEncoded,
        uint cbCertEncoded
    );

    /// <summary>
    /// 打开指定的系统证书存储库 (CertOpenStore)
    /// </summary>
    [DllImport(CRYPT32_LIB, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeCertStoreHandle CertOpenStore(
        IntPtr lpszStoreProvider,
        uint dwMsgAndCertEncodingType,
        IntPtr hCryptProv,
        uint dwFlags,
        string pvPara
    );

    /// <summary>
    /// 将证书上下文添加到证书存储库中 (CertAddCertificateContextToStore)
    /// </summary>
    [DllImport(CRYPT32_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CertAddCertificateContextToStore(
        SafeCertStoreHandle hCertStore,
        SafeCertContextHandle pCertContext,
        uint dwAddDisposition,
        IntPtr ppStoreContext
    );

    /// <summary>
    /// 关闭证书存储库句柄 (CertCloseStore)
    /// </summary>
    [DllImport(CRYPT32_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CertCloseStore(
        IntPtr hCertStore,
        uint dwFlags
    );

    /// <summary>
    /// 释放证书上下文资源 (CertFreeCertificateContext)
    /// </summary>
    [DllImport(CRYPT32_LIB, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CertFreeCertificateContext(
        IntPtr pCertContext
    );
}

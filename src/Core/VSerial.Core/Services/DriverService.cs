using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VSerial.Core.Exceptions;
using VSerial.Core.Logging;
using VSerial.Core.Models;
using VSerial.Interop;

namespace VSerial.Core.Services;

/// <summary>
/// 虚拟串口驱动生命周期与安装部署服务实现类
/// </summary>
public class DriverService : IDriverService
{
    private readonly IVSerialService _vserialService;
    private readonly IVSerialLogger _logger;

    public DriverService(IVSerialService vserialService, IVSerialLogger? logger = null)
    {
        _vserialService = vserialService;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<DriverStatusInfo> GetDriverStatusAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            var pairs = await _vserialService.GetPairsAsync(cancellationToken);
            string? infPath = FindDefaultInfPath();

            var status = new DriverStatusInfo
            {
                InfPath = infPath ?? string.Empty,
                ActivePairsCount = pairs.Count
            };

            bool anyStarted = pairs.Any(p => p.Status == PairStatus.Normal);

            if (anyStarted)
            {
                status.IsInstalled = true;
                status.OperationalStatus = DriverOperationalStatus.Running;
                status.StatusMessage = $"驱动已正常加载并运行 (当前承载 {pairs.Count} 对虚拟串口)";
            }
            else if (pairs.Count > 0)
            {
                status.IsInstalled = true;
                status.OperationalStatus = DriverOperationalStatus.InstalledIdle;
                status.StatusMessage = $"检测到 {pairs.Count} 对已配置虚拟串口，但设备尚未就绪";
            }
            else if (!string.IsNullOrEmpty(infPath))
            {
                status.IsInstalled = true;
                status.OperationalStatus = DriverOperationalStatus.InstalledIdle;
                status.StatusMessage = "驱动安装包就绪，当前暂无已创建的虚拟串口对";
            }
            else
            {
                status.IsInstalled = false;
                status.OperationalStatus = DriverOperationalStatus.NotInstalled;
                status.StatusMessage = "系统尚未安装 VirtualSerial2um 虚拟串口驱动";
            }

            return status;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InstallDriverAsync(string? customInfPath = null, CancellationToken cancellationToken = default)
    {
        if (!NativeMethods.IsProcessElevated())
        {
            throw VSerialException.CreateAccessDenied("安装与注册虚拟串口驱动程序");
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstallDriverInternal(customInfPath);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UninstallDriverAsync(string? customInfPath = null, CancellationToken cancellationToken = default)
    {
        if (!NativeMethods.IsProcessElevated())
        {
            throw VSerialException.CreateAccessDenied("卸载虚拟串口驱动程序");
        }

        // 1. 彻底清空所有虚拟串口节点，释放驱动文件占用的句柄
        _logger.Info("正在清除所有虚拟串口设备节点，以释放驱动文件锁...");
        try
        {
            await _vserialService.DeleteAllPairsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Warning($"清理设备节点时出现提示: {ex.Message}");
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            UninstallDriverInternal(customInfPath);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public string? FindDefaultInfPath()
    {
        string[] searchNames =
        {
            Path.Combine("driver", "virtualserial2um.inf"),
            "virtualserial2um.inf",
            Path.Combine("installer", "driver", "virtualserial2um.inf"),
            Path.Combine("VirtualSerial2um", "virtualserial2um.inf"),
            Path.Combine("src", "Driver", "VirtualSerial2um", "x64", "Release", "VirtualSerial2um", "virtualserial2um.inf"),
            Path.Combine("dist", "VSerial", "virtualserial2um.inf")
        };

        return ProbeFileUpwards(searchNames);
    }

    /// <summary>
    /// 从当前基准目录及当前工作目录出发，逐级向上祖先目录探查指定相对路径列表中的文件
    /// </summary>
    private static string? ProbeFileUpwards(string[] relativeCandidates)
    {
        var rootPaths = new List<string>
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var startPath in rootPaths)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null && dir.Exists)
            {
                foreach (var rel in relativeCandidates)
                {
                    try
                    {
                        string candidate = Path.Combine(dir.FullName, rel);
                        if (File.Exists(candidate))
                        {
                            return Path.GetFullPath(candidate);
                        }
                    }
                    catch
                    {
                        // 忽略格式解析异常
                    }
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    #region 底层 Native 驱动部署实现

    private void InstallDriverInternal(string? customInfPath)
    {
        string? targetInf = !string.IsNullOrWhiteSpace(customInfPath) ? customInfPath : FindDefaultInfPath();
        if (string.IsNullOrEmpty(targetInf) || !File.Exists(targetInf))
        {
            throw new VSerialException(
                VSerialErrorCode.InfFileNotFound,
                $"未找到虚拟串口驱动安装描述文件 virtualserial2um.inf (路径: {targetInf ?? "未指定"})，请确认驱动包完整。",
                "安装虚拟串口驱动"
            );
        }

        string fullInfPath = Path.GetFullPath(targetInf);
        _logger.Info($"使用驱动描述文件: {fullInfPath}");

        // 1. 自动寻找配套证书并导入受信证书存储区
        AutoTrustCertificateIfExists(fullInfPath);

        // 2. 预安装 INF 到 Windows DriverStore
        _logger.Info("正在将驱动程序包预复制到 Windows 驱动存储库 (DriverStore)...");
        var destInf = new StringBuilder(260);
        bool copyOk = NativeMethods.SetupCopyOEMInfW(
            fullInfPath,
            null,
            NativeConstants.SPOST_PATH,
            0,
            destInf,
            (uint)destInf.Capacity,
            out _,
            null
        );

        if (!copyOk)
        {
            uint err = (uint)Marshal.GetLastWin32Error();
            if (err == NativeConstants.CERT_E_UNTRUSTEDROOT)
            {
                throw new VSerialException(
                    VSerialErrorCode.CertificateNotTrusted,
                    "驱动安装失败：测试证书不受系统信任 (Error 0x800B0109: CERT_E_UNTRUSTEDROOT)。" +
                    "请确认已将 VirtualSerial2um.cer 导入至受信任的根证书颁发机构 (ROOT) 与受信任的发布者 (TrustedPublisher)。",
                    "预安装驱动包至 DriverStore",
                    err
                );
            }

            if (err == NativeConstants.ERROR_ACCESS_DENIED)
            {
                throw VSerialException.CreateAccessDenied("预安装驱动包至 DriverStore");
            }

            throw new VSerialException(
                VSerialErrorCode.NativeSetupApiError,
                $"SetupCopyOEMInfW 预安装驱动程序失败，错误码: {err}",
                "预安装驱动包至 DriverStore",
                err
            );
        }

        _logger.Info($"驱动成功预安装到系统 DriverStore，目标 OEM INF: {destInf}");

        // 3. 触发即插即用设备重载刷新
        _logger.Info("正在通知 Windows PnP 子系统更新匹配设备...");
        NativeMethods.UpdateDriverForPlugAndPlayDevicesW(
            IntPtr.Zero,
            NativeConstants.VSERIAL_HARDWARE_ID,
            fullInfPath,
            NativeConstants.INSTALLFLAG_FORCE,
            out _
        );

        _logger.Info("虚拟串口驱动程序包安装并激活完成！");
    }

    private void UninstallDriverInternal(string? customInfPath)
    {
        string? targetInf = !string.IsNullOrWhiteSpace(customInfPath) ? customInfPath : FindDefaultInfPath();
        if (string.IsNullOrEmpty(targetInf) || !File.Exists(targetInf))
        {
            throw new VSerialException(
                VSerialErrorCode.InfFileNotFound,
                "未找到对应的驱动描述文件 INF，无法在 DriverStore 中定位驱动发布包。",
                "卸载虚拟串口驱动"
            );
        }

        string fullInfPath = Path.GetFullPath(targetInf);
        _logger.Info($"正在从 Windows DriverStore 中卸载驱动: {fullInfPath}...");

        bool uninstallOk = NativeMethods.DiUninstallDriverW(
            IntPtr.Zero,
            fullInfPath,
            0,
            out _
        );

        if (!uninstallOk)
        {
            uint err = (uint)Marshal.GetLastWin32Error();
            throw new VSerialException(
                VSerialErrorCode.DriverUninstallFailed,
                $"DiUninstallDriverW 卸载驱动失败，底层错误码: {err}",
                "卸载虚拟串口驱动",
                err
            );
        }

        _logger.Info("虚拟串口驱动包已彻底从系统中卸载！");
    }

    private void AutoTrustCertificateIfExists(string infFullPath)
    {
        string infDir = Path.GetDirectoryName(infFullPath) ?? string.Empty;

        string[] cerNames =
        {
            "VirtualSerial2um.cer",
            "vserial.cer",
            Path.Combine(infDir, "VirtualSerial2um.cer"),
            Path.Combine("installer", "driver", "VirtualSerial2um.cer"),
            Path.Combine("src", "Driver", "VirtualSerial2um", "x64", "Release", "VirtualSerial2um", "VirtualSerial2um.cer"),
            Path.Combine("dist", "VSerial", "VirtualSerial2um.cer")
        };

        string? cerPath = ProbeFileUpwards(cerNames);
        if (string.IsNullOrEmpty(cerPath))
        {
            _logger.Warning("未找到驱动测试签名证书 (.cer)，跳过自动证书信任配置。");
            return;
        }

        _logger.Info($"检测到驱动签名证书: {cerPath}，正在自动导入受信证书库...");
        try
        {
            byte[] certBytes = File.ReadAllBytes(cerPath);
            if (certBytes.Length == 0) return;

            using var certContext = NativeMethods.CertCreateCertificateContext(
                NativeConstants.X509_ASN_ENCODING | NativeConstants.PKCS_7_ASN_ENCODING,
                certBytes,
                (uint)certBytes.Length
            );

            if (certContext.IsInvalid)
            {
                _logger.Warning("证书文件格式解析失败，无法自动信任。");
                return;
            }

            // 导入 LocalMachine\ROOT
            using (var hRoot = NativeMethods.CertOpenStore(
                       (IntPtr)NativeConstants.CERT_STORE_PROV_SYSTEM_W,
                       0,
                       IntPtr.Zero,
                       NativeConstants.CERT_SYSTEM_STORE_LOCAL_MACHINE,
                       "ROOT"))
            {
                if (!hRoot.IsInvalid)
                {
                    NativeMethods.CertAddCertificateContextToStore(
                        hRoot,
                        certContext,
                        NativeConstants.CERT_STORE_ADD_REPLACE_EXISTING,
                        IntPtr.Zero
                    );
                }
            }

            // 导入 LocalMachine\TrustedPublisher
            using (var hPub = NativeMethods.CertOpenStore(
                       (IntPtr)NativeConstants.CERT_STORE_PROV_SYSTEM_W,
                       0,
                       IntPtr.Zero,
                       NativeConstants.CERT_SYSTEM_STORE_LOCAL_MACHINE,
                       "TrustedPublisher"))
            {
                if (!hPub.IsInvalid)
                {
                    NativeMethods.CertAddCertificateContextToStore(
                        hPub,
                        certContext,
                        NativeConstants.CERT_STORE_ADD_REPLACE_EXISTING,
                        IntPtr.Zero
                    );
                }
            }

            _logger.Info("驱动测试证书已自动注入 LocalMachine\\ROOT 及 TrustedPublisher 存储库！");
        }
        catch (Exception ex)
        {
            _logger.Warning($"自动配置证书信任异常 (非致命): {ex.Message}");
        }
    }

    #endregion
}

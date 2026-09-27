using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using VSerial.Core.Exceptions;
using VSerial.Core.Logging;
using VSerial.Core.Models;
using VSerial.Interop;

namespace VSerial.Core.Services;

/// <summary>
/// 虚拟串口核心业务管理服务实现
/// 负责虚拟串口对的枚举、创建、删除、回滚保护与冲突检测
/// </summary>
public class VSerialService : IVSerialService
{
    private readonly IVSerialLogger _logger;
    private readonly SemaphoreSlim _deviceLock = new(1, 1);

    public VSerialService(IVSerialLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VirtualPortPair>> GetPairsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ListPairsInternal();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VirtualPortPair?> GetPairByIdAsync(int pairId, CancellationToken cancellationToken = default)
    {
        var pairs = await GetPairsAsync(cancellationToken);
        return pairs.FirstOrDefault(p => p.PairId == pairId);
    }

    /// <inheritdoc />
    public async Task<VirtualPortPair> CreatePairAsync(int pairId, string portA, string portB, CancellationToken cancellationToken = default)
    {
        if (pairId <= 0)
        {
            throw new VSerialException(
                VSerialErrorCode.InvalidPortName,
                "配对组 ID (Pair ID) 必须为大于 0 的正整数。",
                "校验配对 ID"
            );
        }

        if (!SystemPortDetector.IsValidPortName(portA, out _) || !SystemPortDetector.IsValidPortName(portB, out _))
        {
            throw new VSerialException(
                VSerialErrorCode.InvalidPortName,
                $"端口格式不合法。端口必须命名为 COM1 到 COM256 (输入为 PortA: {portA}, PortB: {portB})。",
                "校验端口名称"
            );
        }

        string normA = SystemPortDetector.NormalizePortName(portA);
        string normB = SystemPortDetector.NormalizePortName(portB);

        if (string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase))
        {
            throw new VSerialException(
                VSerialErrorCode.PortNamesMustDiffer,
                $"成对的端口名称不能相同 (均为 {normA})，请分别指定两个不同的 COM 端口号。",
                "校验端口配对"
            );
        }

        if (!NativeMethods.IsProcessElevated())
        {
            throw VSerialException.CreateAccessDenied($"创建虚拟串口对 {normA} <-> {normB}");
        }

        await _deviceLock.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CreatePairInternal(pairId, normA, normB);
            }, cancellationToken);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeletePairAsync(int pairId, CancellationToken cancellationToken = default)
    {
        if (!NativeMethods.IsProcessElevated())
        {
            throw VSerialException.CreateAccessDenied($"删除虚拟串口配对 ID {pairId}");
        }

        await _deviceLock.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                RemovePairInternal(pairId);
            }, cancellationToken);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAllPairsAsync(CancellationToken cancellationToken = default)
    {
        if (!NativeMethods.IsProcessElevated())
        {
            throw VSerialException.CreateAccessDenied("清除所有虚拟串口配对");
        }

        await _deviceLock.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                RemoveAllPairsInternal();
            }, cancellationToken);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(int nextPairId, string suggestedPortA, string suggestedPortB)> SuggestNextPairAsync(CancellationToken cancellationToken = default)
    {
        var existingPairs = await GetPairsAsync(cancellationToken);
        var occupiedPorts = await GetOccupiedPortsAsync(cancellationToken);

        int nextId = existingPairs.Count > 0 ? existingPairs.Max(p => p.PairId) + 1 : 1;

        // 默认从 COM10 开始寻找两个连续未被占用的端口
        int candidateNum = 10;
        string candidateA = string.Empty;
        string candidateB = string.Empty;

        while (candidateNum < 256)
        {
            string portA = $"COM{candidateNum}";
            string portB = $"COM{candidateNum + 1}";

            if (!occupiedPorts.Contains(portA) && !occupiedPorts.Contains(portB))
            {
                candidateA = portA;
                candidateB = portB;
                break;
            }

            candidateNum += 2;
        }

        if (string.IsNullOrEmpty(candidateA))
        {
            candidateA = "COM10";
            candidateB = "COM11";
        }

        return (nextId, candidateA, candidateB);
    }

    /// <inheritdoc />
    public async Task<ISet<string>> GetOccupiedPortsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var occupied = SystemPortDetector.GetSystemActivePorts();
            var pairs = ListPairsInternal();

            foreach (var pair in pairs)
            {
                if (pair.HasPortA && !string.IsNullOrWhiteSpace(pair.PortA.PortName))
                {
                    occupied.Add(SystemPortDetector.NormalizePortName(pair.PortA.PortName));
                }

                if (pair.HasPortB && !string.IsNullOrWhiteSpace(pair.PortB.PortName))
                {
                    occupied.Add(SystemPortDetector.NormalizePortName(pair.PortB.PortName));
                }
            }

            return (ISet<string>)occupied;
        }, cancellationToken);
    }

    #region 底层 Native 设备操作实现

    private List<VirtualPortPair> ListPairsInternal()
    {
        var pairMap = new Dictionary<int, VirtualPortPair>();

        using var devInfo = NativeMethods.SetupDiGetClassDevsW(
            NativeConstants.GUID_DEVCLASS_PORTS,
            null,
            IntPtr.Zero,
            NativeConstants.DIGCF_PRESENT
        );

        if (devInfo.IsInvalid)
        {
            uint err = (uint)Marshal.GetLastWin32Error();
            _logger.Error($"SetupDiGetClassDevsW 获取设备集失败，Win32 错误码: {err}");
            return new List<VirtualPortPair>();
        }

        var devData = SP_DEVINFO_DATA.Create();
        uint index = 0;

        while (NativeMethods.SetupDiEnumDeviceInfo(devInfo, index, ref devData))
        {
            index++;

            // 1. 读取硬件 ID 匹配 UMDF\VirtualSerial2um
            string? hwId = GetDeviceHardwareId(devInfo, ref devData);
            if (!string.Equals(hwId, NativeConstants.VSERIAL_HARDWARE_ID, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 2. 打开设备注册表参数键读取 PortName, PairId, PairRole
            using var hKey = NativeMethods.SetupDiOpenDevRegKey(
                devInfo,
                ref devData,
                NativeConstants.DICS_FLAG_GLOBAL,
                0,
                NativeConstants.DIREG_DEV,
                NativeConstants.KEY_QUERY_VALUE
            );

            if (hKey.IsInvalid)
            {
                continue;
            }

            uint pairId = ReadDwordRegistryValue(hKey, "PairId");
            uint pairRole = ReadDwordRegistryValue(hKey, "PairRole");
            string portName = ReadStringRegistryValue(hKey, "PortName");

            // 3. 查询设备实例 ID
            var sbInst = new StringBuilder(NativeConstants.MAX_DEVICE_ID_LEN);
            NativeMethods.CM_Get_Device_IDW(devData.DevInst, sbInst, (uint)sbInst.Capacity, 0);
            string instanceId = sbInst.ToString();

            // 4. 查询设备运行状态标志与问题码
            uint status = 0;
            uint problem = 0;
            uint cr = NativeMethods.CM_Get_DevNode_Status(out status, out problem, devData.DevInst, 0);
            bool isStarted = (cr == NativeConstants.CR_SUCCESS && ((status & NativeConstants.DN_STARTED) != 0));

            var devItem = new VirtualPortInfo
            {
                PortName = portName,
                PairId = (int)pairId,
                Role = (PortRole)pairRole,
                InstanceId = instanceId,
                IsStarted = isStarted,
                ProblemCode = problem
            };

            int idKey = (int)pairId;
            if (!pairMap.TryGetValue(idKey, out var pair))
            {
                pair = new VirtualPortPair { PairId = idKey };
                pairMap[idKey] = pair;
            }

            if (pairRole == 0)
            {
                pair.PortA = devItem;
                pair.HasPortA = true;
            }
            else
            {
                pair.PortB = devItem;
                pair.HasPortB = true;
            }
        }

        return pairMap.Values.OrderBy(p => p.PairId).ToList();
    }

    private VirtualPortPair CreatePairInternal(int pairId, string portA, string portB)
    {
        _logger.Info($"开始创建虚拟串口对: Pair {pairId} ({portA} <-> {portB})...");

        // 1. 业务防重检测
        var existingPairs = ListPairsInternal();
        if (existingPairs.Any(p => p.PairId == pairId))
        {
            throw new VSerialException(
                VSerialErrorCode.PairIdAlreadyExists,
                $"配对组 ID {pairId} 已经存在，请指定另一个 Pair ID 或先删除已有配对。",
                $"创建虚拟串口对 Pair {pairId}"
            );
        }

        var occupied = SystemPortDetector.GetSystemActivePorts();
        foreach (var p in existingPairs)
        {
            if (p.HasPortA) occupied.Add(p.PortA.PortName);
            if (p.HasPortB) occupied.Add(p.PortB.PortName);
        }

        if (occupied.Contains(portA))
        {
            throw VSerialException.CreatePortConflict(portA, $"创建虚拟串口对 Pair {pairId}");
        }

        if (occupied.Contains(portB))
        {
            throw VSerialException.CreatePortConflict(portB, $"创建虚拟串口对 Pair {pairId}");
        }

        // 2. 创建 Port A (Role = 0)
        _logger.Info($"正在生成 Port A ({portA}, Role 0)...");
        bool successA = CreatePortNode(portA, (uint)pairId, 0, out string errA);
        if (!successA)
        {
            throw new VSerialException(
                VSerialErrorCode.NativeSetupApiError,
                $"创建端口 A ({portA}) 失败: {errA}",
                $"创建虚拟串口对 Pair {pairId}"
            );
        }

        // 3. 创建 Port B (Role = 1)
        _logger.Info($"正在生成 Port B ({portB}, Role 1)...");
        bool successB = CreatePortNode(portB, (uint)pairId, 1, out string errB);
        if (!successB)
        {
            // 自动原子回滚 Port A
            _logger.Warning($"创建 Port B 失败，正在执行回滚以清理孤立的 Port A ({portA})...");
            try
            {
                RemovePairInternal(pairId);
            }
            catch (Exception ex)
            {
                _logger.Error($"回滚清理 Port A 异常: {ex.Message}", ex);
            }

            throw new VSerialException(
                VSerialErrorCode.DeviceCreationRollback,
                $"创建端口 B ({portB}) 失败，已自动回滚销毁对应的 Port A ({portA})。错误原因: {errB}",
                $"创建虚拟串口对 Pair {pairId}"
            );
        }

        // 4. 触发即插即用驱动更新并静默启动新设备
        TriggerDriverUpdate();

        // 动态轮询等待 Windows PnP 框架加载驱动并创建符号链接 (最长等待 1.5 秒)
        VirtualPortPair? createdPair = null;
        for (int i = 0; i < 15; i++)
        {
            Thread.Sleep(100);
            createdPair = GetPairByIdAsync(pairId).GetAwaiter().GetResult();
            if (createdPair != null && createdPair.Status == PairStatus.Normal)
            {
                break;
            }
        }

        _logger.Info($"成功创建并启动虚拟串口对: Pair {pairId} ({portA} <-> {portB})");

        return createdPair ?? new VirtualPortPair
        {
            PairId = pairId,
            PortA = new VirtualPortInfo { PortName = portA, PairId = pairId, Role = PortRole.RoleA, IsStarted = true },
            PortB = new VirtualPortInfo { PortName = portB, PairId = pairId, Role = PortRole.RoleB, IsStarted = true },
            HasPortA = true,
            HasPortB = true
        };
    }

    private bool CreatePortNode(string portName, uint pairId, uint role, out string errorMsg)
    {
        errorMsg = string.Empty;

        using var devInfo = NativeMethods.SetupDiCreateDeviceInfoList(NativeConstants.GUID_DEVCLASS_PORTS, IntPtr.Zero);
        if (devInfo.IsInvalid)
        {
            errorMsg = $"SetupDiCreateDeviceInfoList 失败，错误码: {Marshal.GetLastWin32Error()}";
            return false;
        }

        var devData = SP_DEVINFO_DATA.Create();
        if (!NativeMethods.SetupDiCreateDeviceInfoW(
                devInfo,
                "Ports",
                NativeConstants.GUID_DEVCLASS_PORTS,
                null,
                IntPtr.Zero,
                NativeConstants.DICD_GENERATE_ID,
                ref devData))
        {
            errorMsg = $"SetupDiCreateDeviceInfoW 失败，错误码: {Marshal.GetLastWin32Error()}";
            return false;
        }

        // 设置硬件 ID: REG_MULTI_SZ 需要以两个 null (\0\0) 结尾
        byte[] hwIdBytes = Encoding.Unicode.GetBytes(NativeConstants.VSERIAL_HARDWARE_ID + "\0\0");
        if (!NativeMethods.SetupDiSetDeviceRegistryPropertyW(
                devInfo,
                ref devData,
                NativeConstants.SPDRP_HARDWAREID,
                hwIdBytes,
                (uint)hwIdBytes.Length))
        {
            errorMsg = $"设置硬件 ID 失败，错误码: {Marshal.GetLastWin32Error()}";
            return false;
        }

        // 注册设备节点
        if (!NativeMethods.SetupDiCallClassInstaller(NativeConstants.DIF_REGISTERDEVICE, devInfo, ref devData))
        {
            errorMsg = $"DIF_REGISTERDEVICE 注册设备失败，错误码: {Marshal.GetLastWin32Error()}";
            return false;
        }

        // 写入设备硬件参数到 Device Parameters 注册表键
        using (var hKey = NativeMethods.SetupDiCreateDevRegKeyW(
                   devInfo,
                   ref devData,
                   NativeConstants.DICS_FLAG_GLOBAL,
                   0,
                   NativeConstants.DIREG_DEV,
                   IntPtr.Zero,
                   null))
        {
            if (hKey.IsInvalid)
            {
                errorMsg = $"SetupDiCreateDevRegKeyW 创建设备注册表键失败，错误码: {Marshal.GetLastWin32Error()}";
                NativeMethods.SetupDiCallClassInstaller(NativeConstants.DIF_REMOVE, devInfo, ref devData);
                return false;
            }

            // 写入 PortName
            byte[] portBytes = Encoding.Unicode.GetBytes(portName + "\0");
            NativeMethods.RegSetValueExW(hKey, "PortName", 0, NativeConstants.REG_SZ, portBytes, (uint)portBytes.Length);

            // 写入 PairId
            byte[] pairIdBytes = BitConverter.GetBytes(pairId);
            NativeMethods.RegSetValueExW(hKey, "PairId", 0, NativeConstants.REG_DWORD, pairIdBytes, (uint)pairIdBytes.Length);

            // 写入 PairRole
            byte[] roleBytes = BitConverter.GetBytes(role);
            NativeMethods.RegSetValueExW(hKey, "PairRole", 0, NativeConstants.REG_DWORD, roleBytes, (uint)roleBytes.Length);
        }

        // 优先尝试从 Windows DriverStore 中直接为新创建的虚拟设备安装驱动 (无感静默快速启动)
        try
        {
            NativeMethods.DiInstallDevice(IntPtr.Zero, devInfo, ref devData, IntPtr.Zero, 0, out _);
        }
        catch (Exception ex)
        {
            _logger.Debug($"单设备 DiInstallDevice 尝试: {ex.Message} (将在后续通过 TriggerDriverUpdate 补偿绑定)");
        }

        return true;
    }

    private void RemovePairInternal(int pairId)
    {
        _logger.Info($"正在查找并删除虚拟串口对 ID {pairId}...");

        using var devInfo = NativeMethods.SetupDiGetClassDevsW(
            NativeConstants.GUID_DEVCLASS_PORTS,
            null,
            IntPtr.Zero,
            NativeConstants.DIGCF_PRESENT
        );

        if (devInfo.IsInvalid)
        {
            throw new VSerialException(
                VSerialErrorCode.NativeSetupApiError,
                $"无法获取设备列表，错误码: {Marshal.GetLastWin32Error()}",
                $"删除虚拟串口配对 ID {pairId}"
            );
        }

        var devData = SP_DEVINFO_DATA.Create();
        uint index = 0;
        int removeCount = 0;

        while (NativeMethods.SetupDiEnumDeviceInfo(devInfo, index, ref devData))
        {
            string? hwId = GetDeviceHardwareId(devInfo, ref devData);
            if (string.Equals(hwId, NativeConstants.VSERIAL_HARDWARE_ID, StringComparison.OrdinalIgnoreCase))
            {
                using var hKey = NativeMethods.SetupDiOpenDevRegKey(
                    devInfo,
                    ref devData,
                    NativeConstants.DICS_FLAG_GLOBAL,
                    0,
                    NativeConstants.DIREG_DEV,
                    NativeConstants.KEY_QUERY_VALUE
                );

                if (!hKey.IsInvalid)
                {
                    uint devPairId = ReadDwordRegistryValue(hKey, "PairId");
                    if (devPairId == (uint)pairId)
                    {
                        if (NativeMethods.SetupDiCallClassInstaller(NativeConstants.DIF_REMOVE, devInfo, ref devData))
                        {
                            removeCount++;
                            continue; // 成功移除后下标无需递增
                        }
                    }
                }
            }

            index++;
        }

        if (removeCount == 0)
        {
            throw new VSerialException(
                VSerialErrorCode.PairNotFound,
                $"未找到配对组 ID 为 {pairId} 的虚拟串口设备，可能该端口已被移除。",
                $"删除虚拟串口配对 ID {pairId}"
            );
        }

        _logger.Info($"成功删除虚拟串口对 ID {pairId} (共销毁 {removeCount} 个端口节点)。");
    }

    private void RemoveAllPairsInternal()
    {
        _logger.Info("正在清空所有虚拟串口对...");

        using var devInfo = NativeMethods.SetupDiGetClassDevsW(
            NativeConstants.GUID_DEVCLASS_PORTS,
            null,
            IntPtr.Zero,
            NativeConstants.DIGCF_PRESENT
        );

        if (devInfo.IsInvalid)
        {
            throw new VSerialException(
                VSerialErrorCode.NativeSetupApiError,
                $"无法获取设备列表，错误码: {Marshal.GetLastWin32Error()}",
                "清空所有虚拟串口对"
            );
        }

        var devData = SP_DEVINFO_DATA.Create();
        uint index = 0;
        int removeCount = 0;

        while (NativeMethods.SetupDiEnumDeviceInfo(devInfo, index, ref devData))
        {
            string? hwId = GetDeviceHardwareId(devInfo, ref devData);
            if (string.Equals(hwId, NativeConstants.VSERIAL_HARDWARE_ID, StringComparison.OrdinalIgnoreCase))
            {
                if (NativeMethods.SetupDiCallClassInstaller(NativeConstants.DIF_REMOVE, devInfo, ref devData))
                {
                    removeCount++;
                    continue;
                }
            }

            index++;
        }

        _logger.Info($"已完成清空操作，累计移除 {removeCount} 个虚拟串口设备。");
    }

    private static string? GetDeviceHardwareId(SafeDeviceInfoSetHandle devInfo, ref SP_DEVINFO_DATA devData)
    {
        byte[] buffer = new byte[512];
        if (NativeMethods.SetupDiGetDeviceRegistryPropertyW(
                devInfo,
                ref devData,
                NativeConstants.SPDRP_HARDWAREID,
                out _,
                buffer,
                (uint)buffer.Length,
                out _))
        {
            string raw = Encoding.Unicode.GetString(buffer);
            int nullPos = raw.IndexOf('\0');
            return nullPos >= 0 ? raw.Substring(0, nullPos) : raw;
        }

        return null;
    }

    private static uint ReadDwordRegistryValue(SafeRegistryHandle hKey, string valueName)
    {
        byte[] buffer = new byte[sizeof(uint)];
        uint size = (uint)buffer.Length;
        uint res = NativeMethods.RegQueryValueExW(hKey, valueName, IntPtr.Zero, out _, buffer, ref size);
        return res == NativeConstants.ERROR_SUCCESS ? BitConverter.ToUInt32(buffer, 0) : 0;
    }

    private static string ReadStringRegistryValue(SafeRegistryHandle hKey, string valueName)
    {
        byte[] buffer = new byte[256];
        uint size = (uint)buffer.Length;
        uint res = NativeMethods.RegQueryValueExW(hKey, valueName, IntPtr.Zero, out _, buffer, ref size);
        if (res == NativeConstants.ERROR_SUCCESS)
        {
            string str = Encoding.Unicode.GetString(buffer, 0, (int)size);
            int nullPos = str.IndexOf('\0');
            return nullPos >= 0 ? str.Substring(0, nullPos) : str;
        }

        return string.Empty;
    }

    private void TriggerDriverUpdate()
    {
        try
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

            string? inf = ProbeFileUpwards(searchNames);
            if (!string.IsNullOrEmpty(inf))
            {
                _logger.Info($"正在触发即插即用驱动更新并重载设备 (INF: {inf})...");
                bool ok = NativeMethods.UpdateDriverForPlugAndPlayDevicesW(
                    IntPtr.Zero,
                    NativeConstants.VSERIAL_HARDWARE_ID,
                    Path.GetFullPath(inf),
                    NativeConstants.INSTALLFLAG_FORCE,
                    out _
                );

                if (!ok)
                {
                    _logger.Warning($"UpdateDriverForPlugAndPlayDevicesW 返回失败，错误码: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    _logger.Info("UpdateDriverForPlugAndPlayDevicesW 设备驱动触发更新成功！");
                }
            }
            else
            {
                _logger.Warning("未找到 virtualserial2um.inf 驱动描述文件，跳过 UpdateDriverForPlugAndPlayDevicesW 触发。");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"触发即插即用驱动更新异常: {ex.Message}");
        }
    }

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

    #endregion
}

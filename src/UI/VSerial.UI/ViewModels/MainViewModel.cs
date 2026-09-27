using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using VSerial.Core.Exceptions;
using VSerial.Core.Models;
using VSerial.Core.Services;
using VSerial.Interop;
using VSerial.UI.Common;
using VSerial.UI.Localization;

namespace VSerial.UI.ViewModels;

/// <summary>
/// 主界面视图模型，协调虚拟串口与驱动的核心业务操作并接入多语言本地化服务
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IVSerialService _vserialService;
    private readonly IDriverService _driverService;

    private ObservableCollection<VirtualPortPair> _pairs = new();
    private VirtualPortPair? _selectedPair;
    private DriverStatusInfo? _driverStatus;
    private bool _isBusy;
    private string _busyMessage = string.Empty;
    private string _statusText = string.Empty;
    private string _lastRefreshTime = string.Empty;
    private bool _isElevated;

    /// <summary>
    /// 弹窗确认委托（可由视图注入或测试 Mock）
    /// </summary>
    public Func<string, string, bool>? ConfirmFunc { get; set; }

    /// <summary>
    /// 弹窗警告提示委托
    /// </summary>
    public Action<string, string>? AlertFunc { get; set; }

    /// <summary>
    /// 打开新建配对对话框委托 (返回 (pairId, portA, portB) 或 null)
    /// </summary>
    public Func<Task<(int pairId, string portA, string portB)?>>? ShowCreateDialogFunc { get; set; }

    // 属性绑定
    public ObservableCollection<VirtualPortPair> Pairs
    {
        get => _pairs;
        set
        {
            if (SetProperty(ref _pairs, value))
            {
                OnPropertyChanged(nameof(HasPairs));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>
    /// 是否存在已配置的虚拟串口对
    /// </summary>
    public bool HasPairs => Pairs.Count > 0;

    /// <summary>
    /// 当前是否无任何虚拟串口对
    /// </summary>
    public bool IsEmpty => Pairs.Count == 0;

    public VirtualPortPair? SelectedPair
    {
        get => _selectedPair;
        set => SetProperty(ref _selectedPair, value);
    }

    public DriverStatusInfo? DriverStatus
    {
        get => _driverStatus;
        set => SetProperty(ref _driverStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string LastRefreshTime
    {
        get => _lastRefreshTime;
        set => SetProperty(ref _lastRefreshTime, value);
    }

    public bool IsElevated
    {
        get => _isElevated;
        set => SetProperty(ref _isElevated, value);
    }

    // 多语言管理支持
    public IReadOnlyList<LanguageInfo> SupportedLanguages => LocalizationManager.SupportedLanguages;

    public LanguageInfo CurrentLanguage
    {
        get => SupportedLanguages.FirstOrDefault(l => string.Equals(l.Code, LocalizationManager.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
               ?? SupportedLanguages[0];
        set
        {
            if (value != null && !string.Equals(value.Code, LocalizationManager.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                LocalizationManager.ApplyLanguage(value.Code);
                OnPropertyChanged(nameof(CurrentLanguage));
            }
        }
    }

    public ICommand ChangeLanguageCommand { get; }

    // 命令绑定
    public ICommand RefreshCommand { get; }
    public ICommand CreatePairCommand { get; }
    public ICommand DeletePairCommand { get; }
    public ICommand DeleteAllPairsCommand { get; }
    public ICommand InstallDriverCommand { get; }
    public ICommand UninstallDriverCommand { get; }
    public ICommand RestartAsAdminCommand { get; }

    /// <summary>
    /// XAML 标记解析与默认运行无参构造函数
    /// </summary>
    public MainViewModel() : this(null, null)
    {
    }

    /// <summary>
    /// 支持依赖注入或单元测试模拟服务的构造函数
    /// </summary>
    public MainViewModel(IVSerialService? vserialService, IDriverService? driverService)
    {
        _vserialService = vserialService ?? new VSerialService();
        _driverService = driverService ?? new DriverService(_vserialService);

        _statusText = LocalizationManager.GetString("Status_Ready", "Ready");
        _lastRefreshTime = LocalizationManager.GetString("Status_NeverUpdated", "Not refreshed yet");

        IsElevated = NativeMethods.IsProcessElevated();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        CreatePairCommand = new AsyncRelayCommand(CreatePairAsync, () => !IsBusy && IsElevated);
        DeletePairCommand = new AsyncRelayCommand(DeleteSelectedPairAsync, () => !IsBusy && IsElevated && SelectedPair != null);
        DeleteAllPairsCommand = new AsyncRelayCommand(DeleteAllPairsAsync, () => !IsBusy && IsElevated && Pairs.Count > 0);
        InstallDriverCommand = new AsyncRelayCommand(InstallDriverAsync, () => !IsBusy && IsElevated);
        UninstallDriverCommand = new AsyncRelayCommand(UninstallDriverAsync, () => !IsBusy && IsElevated);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);

        ChangeLanguageCommand = new RelayCommand(param =>
        {
            if (param is string code)
            {
                LocalizationManager.ApplyLanguage(code);
            }
            else if (param is LanguageInfo lang)
            {
                LocalizationManager.ApplyLanguage(lang.Code);
            }
        });

        LocalizationManager.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged(nameof(DriverStatus));
        OnPropertyChanged(nameof(HasPairs));
        OnPropertyChanged(nameof(IsEmpty));

        if (Pairs != null && Pairs.Count > 0)
        {
            Pairs = new ObservableCollection<VirtualPortPair>(Pairs);
            StatusText = LocalizationManager.Format("Status_ScanSuccess", Pairs.Count);
        }
        else
        {
            StatusText = LocalizationManager.GetString("Status_Ready", "Ready");
        }
    }

    /// <summary>
    /// 刷新当前系统中的虚拟串口配对与驱动状态
    /// </summary>
    public async Task RefreshAsync()
    {
        IsBusy = true;
        BusyMessage = LocalizationManager.GetString("Status_Scanning", "Scanning system ports and driver status...");
        StatusText = LocalizationManager.GetString("Status_Scanning", "Scanning...");

        try
        {
            var pairList = await _vserialService.GetPairsAsync();
            var status = await _driverService.GetDriverStatusAsync();

            Pairs = new ObservableCollection<VirtualPortPair>(pairList);
            DriverStatus = status;
            LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
            StatusText = LocalizationManager.Format("Status_ScanSuccess", Pairs.Count);
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.Format("Status_ScanFailed", ex.Message);
            AlertFunc?.Invoke(
                LocalizationManager.Format("Status_ScanFailed", ex.Message),
                LocalizationManager.GetString("Prompt_Error", "Error")
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 打开对话框并创建新的虚拟串口对
    /// </summary>
    public async Task CreatePairAsync()
    {
        if (ShowCreateDialogFunc == null)
        {
            AlertFunc?.Invoke(
                LocalizationManager.GetString("Prompt_Alert", "Notice"),
                LocalizationManager.GetString("Prompt_Alert", "Notice")
            );
            return;
        }

        var result = await ShowCreateDialogFunc();
        if (result == null)
        {
            return; // 用户取消
        }

        var (pairId, portA, portB) = result.Value;

        IsBusy = true;
        BusyMessage = LocalizationManager.Format("Msg_CreatingPair", pairId, portA, portB);
        StatusText = LocalizationManager.GetString("Msg_CreatingPair", "Creating...");

        try
        {
            await _vserialService.CreatePairAsync(pairId, portA, portB);
            StatusText = LocalizationManager.Format("Msg_CreatePairSuccess", portA, portB);
            await RefreshAsync();
        }
        catch (VSerialException ex)
        {
            StatusText = $"{LocalizationManager.GetString("Msg_CreatePairFailed")}: {ex.Message}";
            AlertFunc?.Invoke(ex.Message, LocalizationManager.GetString("Msg_CreatePairFailed"));
        }
        catch (Exception ex)
        {
            StatusText = $"{LocalizationManager.GetString("Prompt_Error")}: {ex.Message}";
            AlertFunc?.Invoke(ex.Message, LocalizationManager.GetString("Prompt_Error"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 删除指定或选中的虚拟串口对
    /// </summary>
    public async Task DeleteSelectedPairAsync()
    {
        if (SelectedPair == null)
        {
            return;
        }

        await DeletePairAsync(SelectedPair);
    }

    /// <summary>
    /// 删除指定虚拟串口对
    /// </summary>
    public async Task DeletePairAsync(VirtualPortPair pair)
    {
        bool confirm = ConfirmFunc?.Invoke(
            LocalizationManager.Format("Msg_ConfirmDeletePair", pair.PairId, pair.DisplayName),
            LocalizationManager.GetString("Prompt_Confirm", "Confirm Delete")
        ) ?? true;

        if (!confirm)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = LocalizationManager.Format("Msg_DeletingPair", pair.PairId, pair.DisplayName);
        StatusText = LocalizationManager.Format("Msg_DeletingPair", pair.PairId, pair.DisplayName);

        try
        {
            await _vserialService.DeletePairAsync(pair.PairId);
            StatusText = LocalizationManager.Format("Msg_DeletePairSuccess", pair.PairId, pair.DisplayName);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.Format("Msg_DeletePairFailed", ex.Message);
            AlertFunc?.Invoke(
                LocalizationManager.Format("Msg_DeletePairFailed", ex.Message),
                LocalizationManager.GetString("Prompt_Error")
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 清除所有虚拟串口对
    /// </summary>
    public async Task DeleteAllPairsAsync()
    {
        bool confirm = ConfirmFunc?.Invoke(
            LocalizationManager.Format("Msg_ConfirmDeleteAll", Pairs.Count),
            LocalizationManager.GetString("Prompt_Warning", "Warning")
        ) ?? true;

        if (!confirm)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = LocalizationManager.GetString("Msg_DeletingAll");
        StatusText = LocalizationManager.GetString("Msg_DeletingAll");

        try
        {
            await _vserialService.DeleteAllPairsAsync();
            StatusText = LocalizationManager.GetString("Msg_DeleteAllSuccess");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.Format("Msg_DeleteAllFailed", ex.Message);
            AlertFunc?.Invoke(
                LocalizationManager.Format("Msg_DeleteAllFailed", ex.Message),
                LocalizationManager.GetString("Prompt_Error")
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 安装/重新注册虚拟串口驱动与测试证书
    /// </summary>
    public async Task InstallDriverAsync()
    {
        IsBusy = true;
        BusyMessage = LocalizationManager.GetString("Msg_InstallingDriver");
        StatusText = LocalizationManager.GetString("Msg_InstallingDriver");

        try
        {
            await _driverService.InstallDriverAsync();
            StatusText = LocalizationManager.GetString("Msg_DriverInstallSuccess");
            AlertFunc?.Invoke(
                LocalizationManager.GetString("Msg_DriverInstallSuccess"),
                LocalizationManager.GetString("Prompt_Success")
            );
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.Format("Msg_DriverInstallFailed", ex.Message);
            AlertFunc?.Invoke(
                LocalizationManager.Format("Msg_DriverInstallFailed", ex.Message),
                LocalizationManager.GetString("Prompt_Error")
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 彻底卸载驱动程序
    /// </summary>
    public async Task UninstallDriverAsync()
    {
        bool confirm = ConfirmFunc?.Invoke(
            LocalizationManager.GetString("Msg_ConfirmUninstallDriver"),
            LocalizationManager.GetString("Prompt_Warning")
        ) ?? true;

        if (!confirm)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = LocalizationManager.GetString("Msg_UninstallingDriver");
        StatusText = LocalizationManager.GetString("Msg_UninstallingDriver");

        try
        {
            await _driverService.UninstallDriverAsync();
            StatusText = LocalizationManager.GetString("Msg_DriverUninstallSuccess");
            AlertFunc?.Invoke(
                LocalizationManager.GetString("Msg_DriverUninstallSuccess"),
                LocalizationManager.GetString("Prompt_Success")
            );
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.Format("Msg_DriverUninstallFailed", ex.Message);
            AlertFunc?.Invoke(
                LocalizationManager.Format("Msg_DriverUninstallFailed", ex.Message),
                LocalizationManager.GetString("Prompt_Error")
            );
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 提升为管理员权限并重启应用
    /// </summary>
    public void RestartAsAdmin()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(startInfo);
                System.Windows.Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            AlertFunc?.Invoke(
                $"Failed to restart with administrator privileges: {ex.Message}",
                LocalizationManager.GetString("Prompt_Error")
            );
        }
    }
}

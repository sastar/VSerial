using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VSerial.Core.Services;
using VSerial.UI.Common;
using VSerial.UI.Localization;

namespace VSerial.UI.ViewModels;

/// <summary>
/// 创建虚拟串口向导视图模型
/// </summary>
public class CreatePairViewModel : ViewModelBase
{
    private readonly IVSerialService _vserialService;

    private int _pairId = 1;
    private string _portA = "COM10";
    private string _portB = "COM11";
    private string _validationError = string.Empty;
    private bool _isValid = true;
    private bool _isLoading = true;

    private ISet<string> _occupiedPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public int PairId
    {
        get => _pairId;
        set
        {
            if (SetProperty(ref _pairId, value))
            {
                ValidateInputs();
            }
        }
    }

    public string PortA
    {
        get => _portA;
        set
        {
            if (SetProperty(ref _portA, value))
            {
                ValidateInputs();
            }
        }
    }

    public string PortB
    {
        get => _portB;
        set
        {
            if (SetProperty(ref _portB, value))
            {
                ValidateInputs();
            }
        }
    }

    public string ValidationError
    {
        get => _validationError;
        set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    /// <summary>
    /// 是否存在校验错误提示
    /// </summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    public bool IsValid
    {
        get => _isValid;
        set => SetProperty(ref _isValid, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// 候选可用端口列表
    /// </summary>
    public ObservableCollection<string> AvailablePorts { get; } = new();

    /// <summary>
    /// XAML 标记解析与默认运行无参构造函数
    /// </summary>
    public CreatePairViewModel() : this(null)
    {
    }

    /// <summary>
    /// 支持依赖注入或单元测试模拟服务的构造函数
    /// </summary>
    public CreatePairViewModel(IVSerialService? vserialService)
    {
        _vserialService = vserialService ?? new VSerialService();
    }

    /// <summary>
    /// 初始化加载系统端口与推荐分配方案
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            _occupiedPorts = await _vserialService.GetOccupiedPortsAsync();
            var (nextId, suggestedA, suggestedB) = await _vserialService.SuggestNextPairAsync();

            PairId = nextId;
            PortA = suggestedA;
            PortB = suggestedB;

            AvailablePorts.Clear();
            for (int i = 1; i <= 256; i++)
            {
                string portName = $"COM{i}";
                if (!_occupiedPorts.Contains(portName))
                {
                    AvailablePorts.Add(portName);
                }
            }

            ValidateInputs();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 实时前端输入校验与冲突检测
    /// </summary>
    public void ValidateInputs()
    {
        if (PairId <= 0 || PairId > 255)
        {
            ValidationError = LocalizationManager.GetString("Val_PairIdInvalid", "Pair ID must be a positive integer between 1 and 255.");
            IsValid = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(PortA) || string.IsNullOrWhiteSpace(PortB))
        {
            ValidationError = LocalizationManager.GetString("Val_PortEmpty", "Port A and Port B names cannot be empty.");
            IsValid = false;
            return;
        }

        if (!SystemPortDetector.IsValidPortName(PortA, out _))
        {
            ValidationError = LocalizationManager.GetString("Val_PortInvalid", "Port name must be in COMx format (e.g. COM10).");
            IsValid = false;
            return;
        }

        if (!SystemPortDetector.IsValidPortName(PortB, out _))
        {
            ValidationError = LocalizationManager.GetString("Val_PortInvalid", "Port name must be in COMx format (e.g. COM10).");
            IsValid = false;
            return;
        }

        string normA = SystemPortDetector.NormalizePortName(PortA);
        string normB = SystemPortDetector.NormalizePortName(PortB);

        if (string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase))
        {
            ValidationError = LocalizationManager.GetString("Val_PortSame", "Port A and Port B must have different port names.");
            IsValid = false;
            return;
        }

        if (_occupiedPorts.Contains(normA))
        {
            ValidationError = LocalizationManager.Format("Val_PortOccupied", normA);
            IsValid = false;
            return;
        }

        if (_occupiedPorts.Contains(normB))
        {
            ValidationError = LocalizationManager.Format("Val_PortOccupied", normB);
            IsValid = false;
            return;
        }

        ValidationError = string.Empty;
        IsValid = true;
    }
}

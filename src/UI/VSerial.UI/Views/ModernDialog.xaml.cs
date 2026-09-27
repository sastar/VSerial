using System.Windows;
using System.Windows.Media;
using VSerial.UI.Localization;

namespace VSerial.UI.Views;

/// <summary>
/// 现代化弹窗展示类型
/// </summary>
public enum ModernDialogType
{
    Information,
    Success,
    Warning,
    Error,
    Question
}

/// <summary>
/// 现代化无边框拟态对话框后台代码
/// </summary>
public partial class ModernDialog : Window
{
    public string DialogTitle { get; set; } = LocalizationManager.GetString("Prompt_Alert");
    public string MessageText { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "ℹ️";
    public Brush IconBackgroundBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
    public string PrimaryButtonText { get; set; } = LocalizationManager.GetString("Btn_OK");
    public bool IsConfirmMode { get; set; }

    public bool Result { get; private set; }

    public ModernDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// 弹出信息/警告/成功提示框
    /// </summary>
    public static void ShowAlert(Window? owner, string message, string? title = null, ModernDialogType type = ModernDialogType.Information)
    {
        var dialog = new ModernDialog
        {
            Owner = owner ?? Application.Current.MainWindow,
            DialogTitle = title ?? LocalizationManager.GetString("Prompt_Alert"),
            MessageText = message,
            IsConfirmMode = false,
            PrimaryButtonText = LocalizationManager.GetString("Dialog_GotIt")
        };

        switch (type)
        {
            case ModernDialogType.Success:
                dialog.IconGlyph = "✓";
                dialog.IconBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FAE5")); // 翡翠绿底
                dialog.OkBtn.Style = (Style)Application.Current.FindResource("SuccessButtonStyle");
                break;
            case ModernDialogType.Warning:
                dialog.IconGlyph = "⚠️";
                dialog.IconBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")); // 琥珀黄底
                break;
            case ModernDialogType.Error:
                dialog.IconGlyph = "✕";
                dialog.IconBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")); // 玫瑰红底
                dialog.OkBtn.Style = (Style)Application.Current.FindResource("DangerButtonStyle");
                break;
            default:
                dialog.IconGlyph = "ℹ️";
                dialog.IconBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF")); // 天蓝底
                break;
        }

        dialog.ShowDialog();
    }

    /// <summary>
    /// 弹出确认操作询问对话框
    /// </summary>
    public static bool ShowConfirm(Window? owner, string message, string? title = null)
    {
        var dialog = new ModernDialog
        {
            Owner = owner ?? Application.Current.MainWindow,
            DialogTitle = title ?? LocalizationManager.GetString("Prompt_Confirm"),
            MessageText = message,
            IsConfirmMode = true,
            PrimaryButtonText = LocalizationManager.GetString("Dialog_ConfirmProceed"),
            IconGlyph = "❓",
            IconBackgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"))
        };

        bool? result = dialog.ShowDialog();
        return result == true && dialog.Result;
    }
}

using System;
using System.Threading.Tasks;
using System.Windows;
using VSerial.UI.ViewModels;
using VSerial.UI.Views;

namespace VSerial.UI;

/// <summary>
/// 主界面交互后台代码，严格遵循 MVVM 模式，负责原生窗口行为控制与现代化拟态弹窗代理
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = (MainViewModel)DataContext;

        // 挂接现代化操作确认对话框
        _viewModel.ConfirmFunc = (message, title) =>
        {
            return ModernDialog.ShowConfirm(this, message, title);
        };

        // 挂接现代化错误与提示信息对话框
        _viewModel.AlertFunc = (message, title) =>
        {
            var lower = title.ToLowerInvariant();
            var type = lower.Contains("失败") || lower.Contains("错误") || lower.Contains("异常") || lower.Contains("error") || lower.Contains("fail")
                ? ModernDialogType.Error
                : (lower.Contains("成功") || lower.Contains("success") ? ModernDialogType.Success : ModernDialogType.Information);

            ModernDialog.ShowAlert(this, message, title, type);
        };

        // 挂接新建虚拟串口向导窗口
        _viewModel.ShowCreateDialogFunc = () =>
        {
            var dialog = new CreatePairDialog
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result == true && dialog.Result != null)
            {
                return Task.FromResult<(int pairId, string portA, string portB)?>(dialog.Result);
            }

            return Task.FromResult<(int pairId, string portA, string portB)?>(null);
        };

        // 初始化标题栏关闭按钮右上圆角适配
        CloseButton.Tag = new CornerRadius(0, 13, 0, 0);

        // 窗口生命周期事件
        StateChanged += MainWindow_StateChanged;

        Loaded += async (_, _) =>
        {
            await _viewModel.RefreshAsync();
        };
    }

    #region 自定义现代化标题栏窗口控制按钮处理

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            if (MaximizeIconText != null) MaximizeIconText.Text = "❐";
            RootBorder.Margin = new Thickness(0);
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
            RootShadow.Opacity = 0;
            TitleBarBorder.CornerRadius = new CornerRadius(0);
            StatusBarBorder.CornerRadius = new CornerRadius(0);
            if (CloseButton != null) CloseButton.Tag = new CornerRadius(0);
        }
        else
        {
            if (MaximizeIconText != null) MaximizeIconText.Text = "☐";
            RootBorder.Margin = new Thickness(12);
            RootBorder.CornerRadius = new CornerRadius(14);
            RootBorder.BorderThickness = new Thickness(1);
            RootShadow.Opacity = 0.16;
            TitleBarBorder.CornerRadius = new CornerRadius(13, 13, 0, 0);
            StatusBarBorder.CornerRadius = new CornerRadius(0, 0, 13, 13);
            if (CloseButton != null) CloseButton.Tag = new CornerRadius(0, 13, 0, 0);
        }
    }

    #endregion
}
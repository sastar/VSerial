using System.Windows;
using VSerial.UI.ViewModels;

namespace VSerial.UI.Views;

/// <summary>
/// 新建虚拟串口向导窗口后台代码
/// </summary>
public partial class CreatePairDialog : Window
{
    private readonly CreatePairViewModel _viewModel;

    public (int pairId, string portA, string portB)? Result { get; private set; }

    public CreatePairDialog()
    {
        InitializeComponent();
        _viewModel = (CreatePairViewModel)DataContext;

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
        };
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ValidateInputs();
        if (!_viewModel.IsValid)
        {
            return;
        }

        Result = (_viewModel.PairId, _viewModel.PortA, _viewModel.PortB);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
        Close();
    }
}

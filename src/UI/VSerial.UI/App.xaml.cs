using System.Windows;
using VSerial.UI.Localization;

namespace VSerial.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化多语言体系 (根据注册表安装语言、用户配置或系统区域自动决策并热加载字典)
        LocalizationManager.Initialize();
    }
}

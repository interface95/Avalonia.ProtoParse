using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Ursa.Controls;

namespace Avalonia.ProtoParse.Desktop.Views;

public partial class MainWindow : UrsaWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    protected override async Task<bool> CanClose()
    {
        var result = await MessageBox.ShowOverlayAsync("您确定要退出吗？", "退出提示", button: MessageBoxButton.YesNo);
        return result == MessageBoxResult.Yes;
    }
}

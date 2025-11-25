using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Ursa.Controls;

namespace Avalonia.ProtoParse.Desktop.Views;

public partial class MainWindow : UrsaWindow
{
    private readonly string _hostId;

    public MainWindow()
    {
        InitializeComponent();
        
        _hostId = System.Guid.NewGuid().ToString();
        DialogHost.HostId = _hostId;
    }
    
    protected override async Task<bool> CanClose()
    {
        var result = await MessageBox.ShowOverlayAsync("您确定要退出吗？", "退出提示", button: MessageBoxButton.YesNo, hostId: _hostId);
        return result == MessageBoxResult.Yes;
    }
}

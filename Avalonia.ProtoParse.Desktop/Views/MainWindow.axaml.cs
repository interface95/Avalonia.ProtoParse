using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ProtoParse.Desktop.ViewModels;
using Ursa.Controls;

namespace Avalonia.ProtoParse.Desktop.Views;

public partial class MainWindow : UrsaWindow
{
    public MainWindow()
    {
        InitializeComponent();
        InputEditor.TextChanged += InputEditor_TextChanged;
        
        // Register Notification Manager
        NotificationHelper.Notification = new WindowNotificationManager(this)
        {
            MaxItems = 3,
            Position = Avalonia.Controls.Notifications.NotificationPosition.TopRight
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            InputEditor.Text = vm.InputText;
            vm.PropertyChanged += ViewModel_PropertyChanged;
            
            // Inject file dialog logic
            vm.ShowOpenFileDialog = async () => 
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "导入 Protobuf 数据文件",
                    AllowMultiple = false
                });
                return files.FirstOrDefault();
            };
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.InputText) ||
            DataContext is not MainWindowViewModel vm) return;
        if (InputEditor.Text != vm.InputText)
        {
            InputEditor.Text = vm.InputText;
        }
    }

    private void InputEditor_TextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.InputText != InputEditor.Text)
        {
            vm.InputText = InputEditor.Text;
        }
    }
    
    protected override async Task<bool> CanClose()
    {
        var result = await MessageBox.ShowOverlayAsync("您确定要退出吗？", "退出提示", button: MessageBoxButton.YesNo);

        return result == MessageBoxResult.Yes;
    }
}
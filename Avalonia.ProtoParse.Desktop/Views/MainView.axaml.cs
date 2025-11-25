using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.ProtoParse.Desktop.ViewModels;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace Avalonia.ProtoParse.Desktop.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        InputEditor.TextChanged += InputEditor_TextChanged;
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        
        var files = e.Data.GetFiles();
        if (files == null) return;

        var item = files.FirstOrDefault();
        if (item is IStorageFile file)
        {
            await vm.ImportFileAsync(file);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainWindowViewModel vm)
            return;
        // 初始化文本
        if (InputEditor.Text != vm.InputText)
        {
            InputEditor.Text = vm.InputText;
        }
            
        // 监听 VM 属性变化
        vm.PropertyChanged += ViewModel_PropertyChanged;
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
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var topLevel = TopLevel.GetTopLevel(this);

        ArgumentNullException.ThrowIfNull(topLevel);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.Clipboard = topLevel.Clipboard;
            vm.Provider = topLevel.StorageProvider;
            vm.NotificationManager = new WindowNotificationManager(topLevel)
                { MaxItems = 10, Position = NotificationPosition.TopRight };
        }
    }
}

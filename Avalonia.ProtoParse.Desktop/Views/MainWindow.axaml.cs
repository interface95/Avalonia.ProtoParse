using System;
using Avalonia.Controls;
using Avalonia.ProtoParse.Desktop.ViewModels;

namespace Avalonia.ProtoParse.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InputEditor.TextChanged += InputEditor_TextChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            InputEditor.Text = vm.InputText;
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.InputText) && DataContext is MainWindowViewModel vm)
        {
            if (InputEditor.Text != vm.InputText)
            {
                InputEditor.Text = vm.InputText;
            }
        }
    }

    private void InputEditor_TextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.InputText != InputEditor.Text)
            {
                vm.InputText = InputEditor.Text;
            }
        }
    }
}
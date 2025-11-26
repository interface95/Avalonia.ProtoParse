using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.ProtoParse.Desktop.Core;
using Avalonia.ProtoParse.Desktop.ViewModels;
using System.Collections.Generic;
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

        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                InitViewModel(vm);
            }
        };
    }

    private void InitViewModel(MainWindowViewModel vm)
    {
        // Implement the toggle action
        vm.ToggleNodeAction = (node) =>
        {
            if (vm.Source is HierarchicalTreeDataGridSource<ProtoDisplayNode> source)
            {
                var path = FindIndexPath(source.Items, node, IndexPath.Unselected);
                if (path != IndexPath.Unselected)
                {
                    if (node.IsExpanded)
                        source.Collapse(path);
                    else
                        source.Expand(path);
                }
            }
        };

        // Sync expansion state from TreeDataGrid back to Model
        if (vm.Source != null)
        {
            vm.Source.RowExpanded += (sender, args) =>
            {
                if (args.Row.Model is ProtoDisplayNode node)
                    node.IsExpanded = true;
            };
            vm.Source.RowCollapsed += (sender, args) =>
            {
                if (args.Row.Model is ProtoDisplayNode node)
                    node.IsExpanded = false;
            };
        }
    }

    private IndexPath FindIndexPath(IEnumerable<ProtoDisplayNode> nodes, ProtoDisplayNode target, IndexPath currentPath)
    {
        int index = 0;
        foreach (var node in nodes)
        {
            var nextPath = currentPath == IndexPath.Unselected ? new IndexPath(index) : currentPath.Append(index);
            
            if (node == target)
            {
                return nextPath;
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                var found = FindIndexPath(node.Children, target, nextPath);
                if (found != IndexPath.Unselected)
                {
                    return found;
                }
            }
            index++;
        }
        return IndexPath.Unselected;
    }

    protected override async Task<bool> CanClose()
    {
        var result = await MessageBox.ShowOverlayAsync("您确定要退出吗？", "退出提示", button: MessageBoxButton.YesNo, hostId: _hostId);
        return result == MessageBoxResult.Yes;
    }
}

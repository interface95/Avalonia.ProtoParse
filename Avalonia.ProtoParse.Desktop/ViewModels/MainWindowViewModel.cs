using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Layout;
using Avalonia.ProtoParse.Desktop.Views;
using Protobuf.Decode.Parser;
using ReactiveUI;

namespace Avalonia.ProtoParse.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private HierarchicalTreeDataGridSource<ProtoDisplayNode> _source;
    private readonly ObservableCollection<ProtoDisplayNode> _rootNodes = [];
    private List<ProtoDisplayNode> _allNodes = [];

    public bool IsNodeSelected => SelectedNode != null;

    private string _searchText;
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    private string _loadingMessage = "解析中...";
    public string LoadingMessage
    {
        get => _loadingMessage;
        set => this.RaiseAndSetIfChanged(ref _loadingMessage, value);
    }

    public ProtoDisplayNode? SelectedNode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsNodeSelected));
        }
    }

    public string InputText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } =
        "0AA00208AF8EABF5F73210012A99010A1A0A18414E44524F49445F363063656461613162636532666535321235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E6768616932531A5108011A4D0801220E69735F6C6F67696E3D46414C53452A2461323462663833612D373335642D346365332D623935642D303038383233633835393561300350015A0F564F4943455F424F585F4C4F47494E4A2462616631373736322D333835662D346437382D613130302D3166363939316635666664390AE50308BE96B2F5F73210012A9F010A200A18414E44524F49445F3630636564616131626365326665353210C49CABB30F1235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E67686169329102228E020A2462616631373736322D333835662D346437382D613130302D3166363939316635666664391808225F0801220D69735F6C6F67696E3D545255452A2462653833623937382D356336302D346661382D613734302D366663323931393766303630300350015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E4452600802220E69735F6C6F67696E3D46414C53452A2465353331636438392D353237662D346265362D626532342D396135383836326334376665300250015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E445A0E420C4C4F47494E5F425554544F4E650000803F720C4C4F47494E5F524553554C544A2462616631373736322D333835662D346437382D613130302D316636393931663566666439";

    public string StatusText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "就绪";

    public HierarchicalTreeDataGridSource<ProtoDisplayNode> Source
    {
        get => _source;
        set => this.RaiseAndSetIfChanged(ref _source, value);
    }

    public ReactiveCommand<Unit, Unit> ParseCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> ExampleCommand { get; }
    public ReactiveCommand<Unit, Unit> ExpandAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenNewWindowCommand { get; }

    public MainWindowViewModel()
    {
        ParseCommand = ReactiveCommand.CreateFromTask(Parse);
        ClearCommand = ReactiveCommand.Create(Clear);
        ExampleCommand = ReactiveCommand.Create(Example);
        ExpandAllCommand = ReactiveCommand.Create(ExpandAll);
        CollapseAllCommand = ReactiveCommand.Create(CollapseAll);
        OpenNewWindowCommand = ReactiveCommand.Create(OpenNewWindow);

        Source = CreateSource(_rootNodes);
        
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(PerformSearch);
    }

    private void PerformSearch(string? text)
    {
        if (_allNodes.Count == 0) return;

        _rootNodes.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            foreach (var node in _allNodes) _rootNodes.Add(node);
            return;
        }

        var searchText = text.Trim();
        var filtered = FilterNodes(_allNodes, searchText);
        foreach (var node in filtered) _rootNodes.Add(node);
        
        if (filtered.Count > 0)
        {
             ExpandAll();
        }

        (Source?.Selection as ITreeDataGridRowSelectionModel<ProtoDisplayNode>)?.Clear();
    }

    private List<ProtoDisplayNode> FilterNodes(IEnumerable<ProtoDisplayNode> nodes, string searchText)
    {
        var result = new List<ProtoDisplayNode>();
        foreach (var node in nodes)
        {
            var matches = NodeMatches(node, searchText);
            var filteredChildren = FilterNodes(node.Children, searchText);

            if (matches || filteredChildren.Count > 0)
            {
                var newNode = node with 
                { 
                    Children = filteredChildren,
                    IsHighlighted = matches 
                };

                result.Add(newNode);
            }
        }
        return result;
    }

    private bool NodeMatches(ProtoDisplayNode node, string searchText)
    {
        if ((node.Label?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (node.Summary?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (node.FieldDisplay?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        // Fallback: Check RawValue
        // Skip if node has children, as RawValue will contain children's content
        if (node.Children.Count > 0) return false;

        if (node.Node != null && !node.Node.RawValue.IsEmpty)
        {
            try
            {
                // 1. Try standard UTF8
                var text = System.Text.Encoding.UTF8.GetString(node.Node.RawValue.Span);
                if (text.Contains(searchText, StringComparison.OrdinalIgnoreCase)) return true;

                // 2. Try extracting printable ASCII (like 'strings' command)
                // This helps when binary tags mess up UTF8 decoding
                var ascii = new string(node.Node.RawValue.Span.ToArray()
                    .Where(b => b >= 32 && b <= 126)
                    .Select(b => (char)b).ToArray());
                if (ascii.Contains(searchText, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch
            {
                // Ignore errors
            }
        }

        return false;
    }

    private HierarchicalTreeDataGridSource<ProtoDisplayNode> CreateSource(IEnumerable<ProtoDisplayNode> items)
    {
        var cellTemplate =
            (Avalonia.Controls.Templates.IDataTemplate)Application.Current!.FindResource("ProtoNodeTemplate")!;
        
        var source = new HierarchicalTreeDataGridSource<ProtoDisplayNode>(items)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ProtoDisplayNode>(
                    new TemplateColumn<ProtoDisplayNode>(
                        "Field",
                        cellTemplate,
                        null,
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<ProtoDisplayNode>
                        {
                            CompareAscending = (a, b) => string.Compare(a.FieldDisplay, b.FieldDisplay),
                            CompareDescending = (a, b) => string.Compare(b.FieldDisplay, a.FieldDisplay)
                        }),
                    x => x.Children,
                    x => x.Children.Any()),
            }
        };

        var selectionModel = new TreeDataGridRowSelectionModel<ProtoDisplayNode>(source) { SingleSelect = true };
        selectionModel.SelectionChanged += (s, e) =>
        {
            SelectedNode = selectionModel.SelectedItem;
        };
        source.Selection = selectionModel;

        return source;
    }

    private void ExpandAll()
    {
        var items = _source.Items.ToList();
        for (var i = 0; i < items.Count; i++)
        {
            ExpandNodeRecursive(items[i], [i]);
        }
    }

    private void ExpandNodeRecursive(ProtoDisplayNode node, List<int> path)
    {
        if (!node.Children.Any()) return;
        _source?.Expand(new IndexPath(path));
        for (int i = 0; i < node.Children.Count; i++)
        {
            var newPath = new List<int>(path) { i };
            ExpandNodeRecursive(node.Children[i], newPath);
        }
    }

    private void CollapseAll()
    {
        var items = _source.Items.ToList();
        for (var i = 0; i < items.Count; i++)
        {
            Source.Collapse(new IndexPath(i));
            // Optionally recursively collapse if we want deep reset
            CollapseNodeRecursive(items[i], new List<int> { i });
        }
    }

    private void CollapseNodeRecursive(ProtoDisplayNode node, List<int> path)
    {
        if (!node.Children.Any()) return;
        Source.Collapse(new IndexPath(path));
        for (var i = 0; i < node.Children.Count; i++)
        {
            var newPath = new List<int>(path) { i };
            CollapseNodeRecursive(node.Children[i], newPath);
        }
    }

    private async Task Parse()
    {
        if (IsBusy) return;
        IsBusy = true;
        LoadingMessage = "解析中...";
        _rootNodes.Clear();
        StatusText = "解析中...";

        try
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                StatusText = "就绪";
                return;
            }

            var input = InputText;
            var result = await Task.Run(() =>
            {
                // Remove whitespace and newlines
                var cleanHex = input.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
                var data = Convert.FromHexString(cleanHex);
                var nodes = ProtoParser.Parse(data).ToList();
                var displayNodes = ProtoDisplayNode.FromNodes(nodes);
                return (Nodes: displayNodes, Count: nodes.Count);
            });

            StatusText = $"解析成功, 共 {result.Count} 个一级字段";
            _allNodes = result.Nodes.ToList();
            PerformSearch(SearchText);
        }
        catch (Exception ex)
        {
             StatusText = $"解析失败: {ex.Message}";
             var errorNode = ProtoDisplayNode.CreateError($"Parse error: {ex.Message}");
             _rootNodes.Clear();
             _rootNodes.Add(errorNode);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Clear()
    {
        InputText = string.Empty;
        _rootNodes.Clear();
        _allNodes.Clear();
    }

    private void OpenNewWindow()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel()
        };
        window.Show();
    }

    private void Example()
    {
        const string rawText =
            "0AA00208AF8EABF5F73210012A99010A1A0A18414E44524F49445F363063656461613162636532666535321235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E6768616932531A5108011A4D0801220E69735F6C6F67696E3D46414C53452A2461323462663833612D373335642D346365332D623935642D303038383233633835393561300350015A0F564F4943455F424F585F4C4F47494E4A2462616631373736322D333835662D346437382D613130302D3166363939316635666664390AE50308BE96B2F5F73210012A9F010A200A18414E44524F49445F3630636564616131626365326665353210C49CABB30F1235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E67686169329102228E020A2462616631373736322D333835662D346437382D613130302D3166363939316635666664391808225F0801220D69735F6C6F67696E3D545255452A2462653833623937382D356336302D346661382D613734302D366663323931393766303630300350015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E4452600802220E69735F6C6F67696E3D46414C53452A2465353331636438392D353237662D346265362D626532342D396135383836326334376665300250015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E445A0E420C4C4F47494E5F425554544F4E650000803F720C4C4F47494E5F524553554C544A2462616631373736322D333835662D346437382D613130302D316636393931663566666439";
        
        // Split into chunks to avoid AvaloniaEdit single-line rendering issues
        var chunks = Enumerable.Range(0, (int)Math.Ceiling(rawText.Length / 64.0))
            .Select(i => rawText.Substring(i * 64, Math.Min(64, rawText.Length - i * 64)));
            
        InputText = string.Join(Environment.NewLine, chunks);
    }
}
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Platform.Storage;
using Avalonia.ProtoParse.Desktop.Core;
using Avalonia.ProtoParse.Desktop.Helpers;
using Avalonia.ProtoParse.Desktop.Views;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avalonia.ProtoParse.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ObservableCollection<ProtoDisplayNode> _rootNodes = [];
    private List<ProtoDisplayNode> _allNodes = [];

    #region 属性

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNodeSelected))]
    private ProtoDisplayNode? _selectedNode;

    public bool IsNodeSelected => SelectedNode != null;

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _loadingMessage = "解析中...";

    [ObservableProperty] private string _statusText = "就绪";

    [ObservableProperty] private string? _inputText;

    [ObservableProperty] private HierarchicalTreeDataGridSource<ProtoDisplayNode>? _source;

    #endregion

    public MainWindowViewModel()
    {
        Source = CreateSource(_rootNodes);
    }

    #region command

    [RelayCommand]
    private Task OnParseAsync()
    {
        if (IsBusy)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            return RunCommandAsync(() => IsBusy, async () =>
            {
                if (string.IsNullOrWhiteSpace(InputText))
                {
                    StatusText = "就绪";
                    return;
                }

                var data = ParserHelper.ProcessInputText(InputText);
                await ParseDataAsync(data, updateInputText: false);
            }, async error => await HandleParseErrorAsync(error));
        });
    }

    [RelayCommand]
    private void OnClear()
    {
        InputText = string.Empty;
        _rootNodes.Clear();
        _allNodes.Clear();
    }

    [RelayCommand]
    private void OnExample()
    {
        const string rawText =
            "0AA00208AF8EABF5F73210012A99010A1A0A18414E44524F49445F363063656461613162636532666535321235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E6768616932531A5108011A4D0801220E69735F6C6F67696E3D46414C53452A2461323462663833612D373335642D346365332D623935642D303038383233633835393561300350015A0F564F4943455F424F585F4C4F47494E4A2462616631373736322D333835662D346437382D613130302D3166363939316635666664390AE50308BE96B2F5F73210012A9F010A200A18414E44524F49445F3630636564616131626365326665353210C49CABB30F1235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E67686169329102228E020A2462616631373736322D333835662D346437382D613130302D3166363939316635666664391808225F0801220D69735F6C6F67696E3D545255452A2462653833623937382D356336302D346661382D613734302D366663323931393766303630300350015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E4452600802220E69735F6C6F67696E3D46414C53452A2465353331636438392D353237662D346265362D626532342D396135383836326334376665300250015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E445A0E420C4C4F47494E5F425554544F4E650000803F720C4C4F47494E5F524553554C544A2462616631373736322D333835662D346437382D613130302D316636393931663566666439";

        // Split into chunks to avoid AvaloniaEdit single-line rendering issues
        var chunks = Enumerable.Range(0, (int)Math.Ceiling(rawText.Length / 64.0))
            .Select(i => rawText.Substring(i * 64, Math.Min(64, rawText.Length - i * 64)));

        InputText = string.Join(Environment.NewLine, chunks);
    }

    [RelayCommand]
    private void OnExpandAll()
    {
        var items = Source?.Items.ToList();
        if (items == null) return;

        var pathBuffer = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            pathBuffer.Add(i);
            ExpandNodeRecursive(items[i], pathBuffer);
            pathBuffer.RemoveAt(pathBuffer.Count - 1);
        }
    }

    private void ExpandNodeRecursive(ProtoDisplayNode node, List<int> path)
    {
        if (!node.Children.Any()) return;
        Source?.Expand(new IndexPath(path));
        for (var i = 0; i < node.Children.Count; i++)
        {
            path.Add(i);
            ExpandNodeRecursive(node.Children[i], path);
            path.RemoveAt(path.Count - 1);
        }
    }

    [RelayCommand]
    private void OnCollapseAll()
    {
        var items = Source?.Items.ToList();
        if (items == null) return;

        var pathBuffer = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            pathBuffer.Add(i);
            // 先递归收缩子节点
            CollapseNodeRecursive(items[i], pathBuffer);
            // 再收缩当前节点
            Source?.Collapse(new IndexPath(pathBuffer));
            pathBuffer.RemoveAt(pathBuffer.Count - 1);
        }
    }

    private void CollapseNodeRecursive(ProtoDisplayNode node, List<int> path)
    {
        if (!node.Children.Any()) return;
        
        for (var i = 0; i < node.Children.Count; i++)
        {
            path.Add(i);
            // 递归收缩孙节点
            CollapseNodeRecursive(node.Children[i], path);
            // 收缩子节点
            Source?.Collapse(new IndexPath(path));
            path.RemoveAt(path.Count - 1);
        }
    }

    /// <summary>
    /// 打开新窗口
    /// </summary>
    [RelayCommand]
    private void OpenNewWindow()
    {
        var window = new MainWindow
        {
            DataContext = new MainWindowViewModel()
        };
        window.Show();
    }

    /// <summary>
    /// 搜索节点
    /// </summary>
    [RelayCommand]
    private Task OnSearch() => PerformSearchAsync(SearchText);

    /// <summary>
    /// 导入文件
    /// </summary>
    [RelayCommand]
    private async Task OnImportFileAsync()
    {
        var files = await Provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入 Protobuf 数据文件",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        _ = Task.Run(() =>
        {
            return RunCommandAsync(() => IsBusy, async () =>
            {
                IsBusy = true;
                LoadingMessage = "读取文件中...";

                byte[] bytes;
                await using (var stream = await file.OpenReadAsync())
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }

                var data = ParserHelper.ProcessInputBytes(bytes);
                await ParseDataAsync(data, updateInputText: true);
            }, async error => { await NotificationHelper.ShowErrorAsync($"导入失败: {error.Message}"); });
        });
    }

    #endregion

    private async Task ParseDataAsync(byte[] data, bool updateInputText)
    {
        IsBusy = true;
        LoadingMessage = "解析中...";
        StatusText = "解析中...";

        var nodes = ProtoParser.Parse(data).ToList();
        var displayNodes = ProtoDisplayNode.FromNodes(nodes).ToList(); // Materialize list

        LoadingMessage = "加载到视图...";
        StatusText = "加载到视图...";
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (updateInputText)
            {
                InputText = FormatHex(data);
            }

            _rootNodes.Clear();
            StatusText = $"解析成功, 共 {nodes.Count} 个一级字段, 总大小: {data.Length:N0} 字节";
            _allNodes = displayNodes;
            _ = PerformSearchAsync(SearchText);
        });

        await NotificationHelper.ShowSuccessAsync($"解析成功，共 {nodes.Count} 个一级字段");

        IsBusy = false;
    }

    private static string FormatHex(byte[] data)
    {
        if (data.Length == 0) return string.Empty;
        
        // 限制最大显示大小，防止界面卡死 (例如限制为 5MB 数据 -> 10MB 文本)
        // 如果用户一定要看大文件，建议使用专门的 Hex 编辑器控件
        const int maxBytesToDisplay = 20 * 1024 * 1024;
        
        var displayData = data;
        var isTruncated = false;
        
        if (data.Length > maxBytesToDisplay)
        {
            displayData = data[..maxBytesToDisplay];
            isTruncated = true;
        }

        const int bytesPerLine = 42;
        var sb = new StringBuilder(displayData.Length * 2 + (displayData.Length / bytesPerLine) * 2);
        
        for (var i = 0; i < displayData.Length; i += bytesPerLine)
        {
            if (i > 0) sb.AppendLine();
            var count = Math.Min(bytesPerLine, displayData.Length - i);
            sb.Append(Convert.ToHexString(displayData, i, count));
        }

        if (!isTruncated) 
            return sb.ToString();
        
        sb.AppendLine();
        sb.AppendLine();
        sb.Append($"// ... (数据过大，仅显示前 {maxBytesToDisplay/1024} KB，共 {data.Length/1024} KB)");

        return sb.ToString();
    }

    private async Task HandleParseErrorAsync(Exception ex)
    {
        StatusText = $"解析失败: {ex.Message}";
        var errorNode = ProtoDisplayNode.CreateError($"Parse error: {ex.Message}");
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _rootNodes.Clear();
            _rootNodes.Add(errorNode);
        });

        await NotificationHelper.ShowErrorAsync($"解析失败: {ex.Message}");
        IsBusy = false;
    }

    private async Task PerformSearchAsync(string? text)
    {
        if (_allNodes.Count == 0) return;

        _rootNodes.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            foreach (var node in _allNodes) _rootNodes.Add(node);
            return;
        }

        var searchText = text.Trim();
        var context = SearchContext.Create(searchText);
        var filtered = FilterNodes(_allNodes, context);
        foreach (var node in filtered)
            _rootNodes.Add(node);

        var matchCount = CountTotalMatches(filtered);
        if (matchCount > 0)
        {
            OnExpandAll();
            await NotificationHelper.ShowInfoAsync($"搜索完成，找到 {matchCount} 个匹配项");
        }
        else
        {
            await NotificationHelper.ShowInfoAsync("未找到匹配项");
        }

        (Source?.Selection as ITreeDataGridRowSelectionModel<ProtoDisplayNode>)?.Clear();
    }

    private int CountTotalMatches(IEnumerable<ProtoDisplayNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node.IsHighlighted) count++;
            count += CountTotalMatches(node.Children);
        }
        return count;
    }

    private List<ProtoDisplayNode> FilterNodes(IEnumerable<ProtoDisplayNode> nodes, SearchContext context)
    {
        return (from node in nodes
            let matches = NodeMatches(node, context)
            let filteredChildren = FilterNodes(node.Children, context)
            where matches || filteredChildren.Count > 0
            select node with { Children = filteredChildren, IsHighlighted = matches }).ToList();
    }

    private bool NodeMatches(ProtoDisplayNode node, SearchContext context)
    {
        if (node.Label.Contains(context.Text, StringComparison.OrdinalIgnoreCase) ||
            node.Summary.Contains(context.Text, StringComparison.OrdinalIgnoreCase) ||
            node.FieldDisplay.Contains(context.Text, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (node.Children.Count > 0 || node.Node == null || node.Node.RawValue.IsEmpty)
            return false;

        return RawValueMatches(node.Node.RawValue.Span, context);
    }

    private bool RawValueMatches(ReadOnlySpan<byte> rawValue, SearchContext context)
    {
        if (rawValue.IsEmpty || context.SearchRunes.Length == 0)
            return false;

        if (Utf8ContainsOrdinalIgnoreCase(rawValue, context.SearchRunes))
            return true;

        return context.CanUseAsciiFallback && ContainsPrintableAscii(rawValue, context.SearchRunes);
    }

    private static bool ContainsPrintableAscii(ReadOnlySpan<byte> data, ReadOnlySpan<Rune> searchRunes)
    {
        if (data.IsEmpty)
            return false;

        var pool = ArrayPool<byte>.Shared;
        var rented = pool.Rent(data.Length);
        try
        {
            var count = 0;
            foreach (var b in data)
            {
                if (IsPrintableAscii(b))
                {
                    rented[count++] = b;
                }
            }

            return count != 0 && Utf8ContainsOrdinalIgnoreCase(rented.AsSpan(0, count), searchRunes);
        }
        finally
        {
            pool.Return(rented);
        }
    }

    private static bool Utf8ContainsOrdinalIgnoreCase(ReadOnlySpan<byte> source, ReadOnlySpan<Rune> searchRunes)
    {
        if (searchRunes.IsEmpty)
            return false;

        var offset = 0;
        while (offset < source.Length)
        {
            if (!TryDecodeRune(source[offset..], out _, out var consumedAtStart))
            {
                offset++;
                continue;
            }

            if (StartsWithRunes(source[offset..], searchRunes))
                return true;

            offset += consumedAtStart;
        }

        return false;
    }

    private static bool StartsWithRunes(ReadOnlySpan<byte> source, ReadOnlySpan<Rune> expectedRunes)
    {
        var offset = 0;
        foreach (var t in expectedRunes)
        {
            if (!TryDecodeRune(source[offset..], out var rune, out var consumed))
                return false;

            if (Rune.ToUpperInvariant(rune) != t)
                return false;

            offset += consumed;
        }

        return true;
    }

    private static bool TryDecodeRune(ReadOnlySpan<byte> source, out Rune rune, out int consumed)
    {
        var status = Rune.DecodeFromUtf8(source, out rune, out consumed);
        if (status == OperationStatus.Done)
            return true;

        rune = default;
        consumed = 0;
        return false;
    }

    private static bool IsPrintableAscii(byte value) => value is >= 32 and <= 126;

    private readonly record struct SearchContext(string Text, Rune[] SearchRunes, bool CanUseAsciiFallback)
    {
        public static SearchContext Create(string text)
        {
            var runes = text.EnumerateRunes()
                .Select(static r => Rune.ToUpperInvariant(r))
                .ToArray();
            var canUseAsciiFallback = text.All(static c => c is >= ' ' and <= '~');
            return new SearchContext(text, runes, canUseAsciiFallback);
        }
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
        selectionModel.SelectionChanged += (s, e) => { SelectedNode = selectionModel.SelectedItem; };
        source.Selection = selectionModel;

        return source;
    }
}
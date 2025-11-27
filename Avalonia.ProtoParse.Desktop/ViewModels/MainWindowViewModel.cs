using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.ProtoParse.Desktop.Core;
using Avalonia.ProtoParse.Desktop.Helpers;
using Avalonia.ProtoParse.Desktop.Views;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;

namespace Avalonia.ProtoParse.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public WindowNotificationManager? NotificationManager { get; set; }

    private readonly ObservableCollection<ProtoDisplayNode> _rootNodes = [];
    private List<ProtoDisplayNode> _allNodes = [];
    private static readonly ConditionalWeakTable<ProtoNode, byte[]> PrintableAsciiCache = new();

    #region 属性

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNodeSelected))]
    private ProtoDisplayNode? _selectedNode;

    public bool IsNodeSelected => SelectedNode != null;

    [ObservableProperty] private string _searchText = "";

    /// <summary>
    /// 当搜索框清空时自动恢复完整结果
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = PerformSearchAsync(string.Empty);
        }
    }

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _loadingMessage = "解析中...";

    [ObservableProperty] private string _statusText = "就绪";

    [ObservableProperty] private string? _inputText;

    [ObservableProperty] private HierarchicalTreeDataGridSource<ProtoDisplayNode>? _source;

    #endregion

    /// <summary>
    /// 初始化视图模型并配置树形数据源
    /// </summary>
    public MainWindowViewModel()
    {
        Source = CreateSource(_rootNodes);
    }

    public Action<ProtoDisplayNode>? ToggleNodeAction { get; set; }

    [RelayCommand]
    private void ToggleNode(ProtoDisplayNode node)
    {
        ToggleNodeAction?.Invoke(node);
    }

    #region command

    /// <summary>
    /// 解析输入文本中的 Protobuf 数据
    /// </summary>
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

    /// <summary>
    /// 清空输入内容与解析结果
    /// </summary>
    [RelayCommand]
    private void OnClear()
    {
        InputText = string.Empty;
        _rootNodes.Clear();
        _allNodes.Clear();
    }

    /// <summary>
    /// 填充示例十六进制字符串以便演示
    /// </summary>
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

    /// <summary>
    /// 展开树中的全部根节点及其子节点
    /// </summary>
    [RelayCommand]
    private void OnExpandAll()
    {
        if (_rootNodes.Count == 0) return;
        UpdateExpansionState(_rootNodes, true);
        RefreshTreeView();
    }

    /// <summary>
    /// 收起树中的全部节点
    /// </summary>
    [RelayCommand]
    private void OnCollapseAll()
    {
        if (_rootNodes.Count == 0) return;
        UpdateExpansionState(_rootNodes, false);
        RefreshTreeView();
    }

    /// <summary>
    /// 递归设置节点的展开状态
    /// </summary>
    private static void UpdateExpansionState(IEnumerable<ProtoDisplayNode> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = isExpanded;
            if (node.Children.Count > 0)
            {
                UpdateExpansionState(node.Children, isExpanded);
            }
        }
    }

    /// <summary>
    /// 通过重建根集合来触发 TreeDataGrid 刷新
    /// </summary>
    private void RefreshTreeView()
    {
        var snapshot = _rootNodes.ToList();
        _rootNodes.Clear();
        foreach (var node in snapshot)
        {
            _rootNodes.Add(node);
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
        if (Provider is null)
            return;

        var files = await Provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入 Protobuf 数据文件",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.All]
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        await ImportFileAsync(file);
    }

    /// <summary>
    /// 导入选中文件并触发后台解析
    /// </summary>
    public Task ImportFileAsync(IStorageFile file)
    {
        return Task.Run(() =>
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
            }, async error => { await NotificationManager.ShowErrorAsync($"导入失败: {error.Message}"); });
        });
    }

    /// <summary>
    /// 导出节点数据
    /// </summary>
    [RelayCommand]
    private async Task OnExportNodeAsync(ProtoDisplayNode? node)
    {
        node ??= SelectedNode;
        if (node is null || Provider is null) return;

        var fileName = $"node_{node.FieldNumber}_{node.WireType}";

        var file = await Provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出节点数据",
            SuggestedFileName = fileName,
            FileTypeChoices =
            [
                new FilePickerFileType("原始二进制") { Patterns = ["*.bin"] },
                new FilePickerFileType("JSON 结构") { Patterns = ["*.json"] },
                new FilePickerFileType("Hex 文本") { Patterns = ["*.txt"] }
            ]
        });

        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);

            if (file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(node.ToJsonObject(), ProtoJsonContext.Default.ProtoNodeDto);
                await writer.WriteAsync(json);
            }
            else if (file.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                var hex = node.Node is { RawValue.IsEmpty: false }
                    ? Convert.ToHexString(node.Node.RawValue.Span)
                    : string.Empty;
                await writer.WriteAsync(hex);
            }
            else // .bin
            {
                if (node.Node is { RawValue.IsEmpty: false })
                {
                    await stream.WriteAsync(node.Node.RawValue);
                }
            }

            await NotificationManager.ShowSuccessAsync("导出成功");
        }
        catch (Exception ex)
        {
            await NotificationManager.ShowErrorAsync($"导出失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyNodeContent(ProtoDisplayNode? node)
    {
        node ??= SelectedNode;
        if (node is null) return;

        // 优先复制 UTF8 预览，如果没有则复制 Hex
        var content = node.Utf8Preview ?? node.RawPreview;
        if (string.IsNullOrEmpty(content)) return;

        await CopyToClipboardAsync(content);
        await NotificationManager.ShowSuccessAsync("已复制节点内容");
    }

    [RelayCommand]
    private async Task CopyNodeHex(ProtoDisplayNode? node)
    {
        node ??= SelectedNode;
        if (node is null) return;

        var hex = node.Node is { RawValue.IsEmpty: false }
            ? Convert.ToHexString(node.Node.RawValue.Span)
            : string.Empty;

        if (string.IsNullOrEmpty(hex)) return;

        await CopyToClipboardAsync(hex);
        await NotificationManager.ShowSuccessAsync("已复制节点 Hex");
    }

    [RelayCommand]
    private async Task CopyNodePath(ProtoDisplayNode? node)
    {
        node ??= SelectedNode;
        if (node is null) return;

        if (string.IsNullOrEmpty(node.Path)) return;

        await CopyToClipboardAsync(node.Path);
        await NotificationManager.ShowSuccessAsync("已复制节点路径");
    }

    private async Task CopyToClipboardAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { Clipboard: { } clipboard } })
        {
            await clipboard.SetTextAsync(text);
        }
    }

    #endregion

    /// <summary>
    /// 将解析后的节点绑定到界面并更新状态
    /// </summary>
    private async Task ParseDataAsync(byte[] data, bool updateInputText)
    {
        IsBusy = true;
        LoadingMessage = "解析中...";
        StatusText = "解析中...";

        var nodes = ProtoParser.Parse(data).ToList();
        var displayNodes = ProtoDisplayNode.FromNodes(nodes, CopyNodeContentCommand, CopyNodeHexCommand, CopyNodePathCommand).ToList(); // Materialize list

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

        await NotificationManager.ShowSuccessAsync($"解析成功，共 {nodes.Count} 个一级字段");

        IsBusy = false;
    }

    /// <summary>
    /// 将字节数组格式化为带换行的十六进制字符串
    /// </summary>
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
        sb.Append($"// ... (数据过大，仅显示前 {maxBytesToDisplay / 1024} KB，共 {data.Length / 1024} KB)");

        return sb.ToString();
    }

    /// <summary>
    /// 统一处理解析失败时的状态与提示
    /// </summary>
    private async Task HandleParseErrorAsync(Exception ex)
    {
        StatusText = $"解析失败: {ex.Message}";
        var errorNode = ProtoDisplayNode.CreateError($"Parse error: {ex.Message}");
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _rootNodes.Clear();
            _rootNodes.Add(errorNode);
        });

        await NotificationManager.ShowErrorAsync($"解析失败: {ex.Message}");
        IsBusy = false;
    }

    /// <summary>
    /// 根据关键字过滤节点并显示结果
    /// </summary>
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
        var filtered = FilterNodes(_allNodes, context, expandMatchedPaths: true);
        foreach (var node in filtered)
            _rootNodes.Add(node);

        var matchCount = CountTotalMatches(filtered);
        if (matchCount > 0)
        {
            await NotificationManager.ShowInfoAsync($"搜索完成，找到 {matchCount} 个匹配项");
        }
        else
        {
            await NotificationManager.ShowInfoAsync("未找到匹配项");
        }

        (Source?.Selection as ITreeDataGridRowSelectionModel<ProtoDisplayNode>)?.Clear();
    }

    /// <summary>
    /// 统计树中所有高亮节点的数量
    /// </summary>
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

    /// <summary>
    /// 递归过滤节点集合，保留匹配项
    /// </summary>
    private List<ProtoDisplayNode> FilterNodes(IEnumerable<ProtoDisplayNode> nodes, SearchContext context, bool expandMatchedPaths)
    {
        var result = new List<ProtoDisplayNode>();
        foreach (var node in nodes)
        {
            var matches = NodeMatches(node, context);
            var filteredChildren = FilterNodes(node.Children, context, expandMatchedPaths);
            if (!matches && filteredChildren.Count <= 0)
                continue;

            var copy = node with { Children = filteredChildren, IsHighlighted = matches };
            copy.IsExpanded = expandMatchedPaths && filteredChildren.Count > 0;
            result.Add(copy);
        }

        return result;
    }

    /// <summary>
    /// 检查当前节点的显示文本或原始值是否匹配搜索条件
    /// </summary>
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

        return RawValueMatches(node.Node, context);
    }

    /// <summary>
    /// 在节点原始字节数据中匹配关键字
    /// </summary>
    private bool RawValueMatches(ProtoNode protoNode, SearchContext context)
    {
        var rawValue = protoNode.RawValue.Span;
        if (rawValue.IsEmpty || context.SearchRunes.Length == 0)
            return false;

        if (Utf8ContainsOrdinalIgnoreCase(rawValue, context.SearchRunes))
            return true;

        if (!context.CanUseAsciiFallback)
            return false;

        var ascii = GetPrintableAscii(protoNode);
        return !ascii.IsEmpty && Utf8ContainsOrdinalIgnoreCase(ascii, context.SearchRunes);
    }

    /// <summary>
    /// 获取节点中可打印 ASCII 的缓存切片
    /// </summary>
    private static ReadOnlySpan<byte> GetPrintableAscii(ProtoNode node)
    {
        var cached = PrintableAsciiCache.GetValue(node, static n =>
        {
            var span = n.RawValue.Span;
            if (span.IsEmpty)
                return [];

            var pool = ArrayPool<byte>.Shared;
            var rented = pool.Rent(span.Length);
            var count = 0;
            foreach (var b in span)
            {
                if (IsPrintableAscii(b))
                {
                    rented[count++] = b;
                }
            }

            byte[] result;
            if (count == 0)
            {
                result = Array.Empty<byte>();
            }
            else
            {
                result = new byte[count];
                Buffer.BlockCopy(rented, 0, result, 0, count);
            }

            pool.Return(rented);
            return result;
        });

        return cached.AsSpan();
    }

    /// <summary>
    /// 判断 UTF-8 序列是否包含指定 Rune 序列（忽略大小写）
    /// </summary>
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

    /// <summary>
    /// 判断字节序列开头是否与目标 Rune 序列一致
    /// </summary>
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

    /// <summary>
    /// 从 UTF-8 字节序列尝试解码一个 Rune
    /// </summary>
    private static bool TryDecodeRune(ReadOnlySpan<byte> source, out Rune rune, out int consumed)
    {
        var status = Rune.DecodeFromUtf8(source, out rune, out consumed);
        if (status == OperationStatus.Done)
            return true;

        rune = default;
        consumed = 0;
        return false;
    }

    /// <summary>
    /// 判断字节是否为可打印 ASCII 字符
    /// </summary>
    private static bool IsPrintableAscii(byte value) => value is >= 32 and <= 126;

    private readonly record struct SearchContext(string Text, Rune[] SearchRunes, bool CanUseAsciiFallback)
    {
        /// <summary>
        /// 根据原始搜索文本生成用于匹配的上下文
        /// </summary>
        public static SearchContext Create(string text)
        {
            var runes = text.EnumerateRunes()
                .Select(static r => Rune.ToUpperInvariant(r))
                .ToArray();
            var canUseAsciiFallback = text.All(static c => c is >= ' ' and <= '~');
            return new SearchContext(text, runes, canUseAsciiFallback);
        }
    }

    /// <summary>
    /// 创建树形网格数据源并配置列及选择逻辑
    /// </summary>
    private HierarchicalTreeDataGridSource<ProtoDisplayNode> CreateSource(IEnumerable<ProtoDisplayNode> items)
    {
        var cellTemplate =
            (Controls.Templates.IDataTemplate)Application.Current!.FindResource("ProtoNodeTemplate")!;

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
                    x => x.Children.Any(),
                    x => x.IsExpanded),
            }
        };

        var selectionModel = new TreeDataGridRowSelectionModel<ProtoDisplayNode>(source) { SingleSelect = true };
        selectionModel.SelectionChanged += (s, e) => { SelectedNode = selectionModel.SelectedItem; };
        source.Selection = selectionModel;

        return source;
    }
}

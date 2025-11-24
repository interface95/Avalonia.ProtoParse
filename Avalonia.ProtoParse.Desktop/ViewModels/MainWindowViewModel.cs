using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Layout;
using Protobuf.Decode.Parser;
using ReactiveUI;

namespace Avalonia.ProtoParse.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _inputText = "0AA00208AF8EABF5F73210012A99010A1A0A18414E44524F49445F363063656461613162636532666535321235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E6768616932531A5108011A4D0801220E69735F6C6F67696E3D46414C53452A2461323462663833612D373335642D346365332D623935642D303038383233633835393561300350015A0F564F4943455F424F585F4C4F47494E4A2462616631373736322D333835662D346437382D613130302D3166363939316635666664390AE50308BE96B2F5F73210012A9F010A200A18414E44524F49445F3630636564616131626365326665353210C49CABB30F1235082010011A027A682204584459582A0C362E31312E302E313031313830864F3A12636F6D2E736D696C652E6769666D616B657248031A170A02323512115869616F6475285844482D31382D41312922100802120CE4B8ADE59BBDE7A7BBE58AA83A191A17474D542B30383A303020417369612F5368616E67686169329102228E020A2462616631373736322D333835662D346437382D613130302D3166363939316635666664391808225F0801220D69735F6C6F67696E3D545255452A2462653833623937382D356336302D346661382D613734302D366663323931393766303630300350015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E4452600802220E69735F6C6F67696E3D46414C53452A2465353331636438392D353237662D346265362D626532342D396135383836326334376665300250015A22564F4943455F424F585F4C414E4453434150455F564F4943455F424F585F46494E445A0E420C4C4F47494E5F425554544F4E650000803F720C4C4F47494E5F524553554C544A2462616631373736322D333835662D346437382D613130302D316636393931663566666439";
    private HierarchicalTreeDataGridSource<ProtoDisplayNode>? _source;
    private string _statusText = "就绪";

    public string InputText
    {
        get => _inputText;
        set => this.RaiseAndSetIfChanged(ref _inputText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public HierarchicalTreeDataGridSource<ProtoDisplayNode>? Source
    {
        get => _source;
        set => this.RaiseAndSetIfChanged(ref _source, value);
    }

    public ReactiveCommand<Unit, Unit> ParseCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> ExpandAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CollapseAllCommand { get; }

    public MainWindowViewModel()
    {
        ParseCommand = ReactiveCommand.Create(Parse);
        ClearCommand = ReactiveCommand.Create(Clear);
        ExpandAllCommand = ReactiveCommand.Create(ExpandAll);
        CollapseAllCommand = ReactiveCommand.Create(CollapseAll);

        Parse();
    }

    private void ExpandAll()
    {
        if (_source == null) return;
        var items = _source.Items.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            ExpandNodeRecursive(items[i], new List<int> { i });
        }
    }

    private void ExpandNodeRecursive(ProtoDisplayNode node, List<int> path)
    {
        if (node.Children.Any())
        {
            _source?.Expand(new IndexPath(path));
            for (int i = 0; i < node.Children.Count; i++)
            {
                var newPath = new List<int>(path) { i };
                ExpandNodeRecursive(node.Children[i], newPath);
            }
        }
    }

    private void CollapseAll()
    {
        if (_source == null) return;
        var items = _source.Items.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            _source.Collapse(new IndexPath(i));
            // Optionally recursively collapse if we want deep reset
            CollapseNodeRecursive(items[i], new List<int> { i });
        }
    }

    private void CollapseNodeRecursive(ProtoDisplayNode node, List<int> path)
    {
        if (node.Children.Any())
        {
            _source?.Collapse(new IndexPath(path));
            for (int i = 0; i < node.Children.Count; i++)
            {
                var newPath = new List<int>(path) { i };
                CollapseNodeRecursive(node.Children[i], newPath);
            }
        }
    }

    private void Parse()
    {
        try 
        {
            if (string.IsNullOrWhiteSpace(InputText))
            {
                Source = null;
                return;
            }

            // Remove whitespace and newlines
            var cleanHex = InputText.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
            var data = Convert.FromHexString(cleanHex);
            var nodes = ProtoParser.Parse(data).ToList();
            StatusText = $"解析成功, 共 {nodes.Count} 个一级字段";
            var displayNodes = ProtoDisplayNode.FromNodes(nodes);

            var cellTemplate = (Avalonia.Controls.Templates.IDataTemplate)Application.Current!.FindResource("ProtoNodeTemplate")!;

            Source = new HierarchicalTreeDataGridSource<ProtoDisplayNode>(displayNodes)
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
        }
        catch (Exception ex)
        {
             StatusText = $"解析失败: {ex.Message}";
             var errorNode = ProtoDisplayNode.CreateError($"Parse error: {ex.Message}");
             Source = new HierarchicalTreeDataGridSource<ProtoDisplayNode>(new[] { errorNode })
             {
                 Columns = 
                 {
                     new TextColumn<ProtoDisplayNode, string>("Error", x => x.Label)
                 }
             };
        }
    }

    private void Clear()
    {
        InputText = string.Empty;
        Source = null;
    }
}
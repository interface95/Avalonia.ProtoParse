using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace Protobuf.Decode.Parser;

public sealed record ProtoDisplayNode(
    string Label,
    ProtoNode? Node,
    string Path,
    IReadOnlyList<ProtoDisplayNode> Children,
    int FieldNumber,
    ProtoWireType WireType,
    string Summary,
    string RawPreview)
{
    public bool IsError => Node is null && Children.Count == 0;
    public bool IsRepeated => TryGetOccurrenceIndexFromSegment(GetLastSegment(Path), out _);
    public int OccurrenceIndex => TryGetOccurrenceIndexFromSegment(GetLastSegment(Path), out var index) ? index : 1;
    public string FieldDisplay => FormatFieldDisplay();
    public string WireTypeDisplay => WireType.ToString();
    public bool IsArrayGroup => Node is null && Children.Count > 0;

    public ProtoDisplayNode(ProtoNode node, string path)
        : this(CreateLabel(node), node, path,
            CreateChildren(node, path),
            node.FieldNumber,
            node.WireType,
            CreateSummary(node),
            CreateRawPreview(node))
    {
    }

    public static IReadOnlyList<ProtoDisplayNode> FromNodes(IReadOnlyList<ProtoNode> nodes)
        => BuildDisplayNodes(nodes, string.Empty);

    private static IReadOnlyList<ProtoDisplayNode> CreateChildren(ProtoNode node, string parentPath)
        => node.Children is { Count: > 0 }
            ? BuildDisplayNodes(node.Children, parentPath)
            : Array.Empty<ProtoDisplayNode>();

    public static ProtoDisplayNode CreateError(string message)
        => new(message, null, string.Empty, Array.Empty<ProtoDisplayNode>(), -1, 0, message, string.Empty);

    public static long VarintToValue(ReadOnlySpan<byte> span)
    {
        ulong result = 0;
        int shift = 0;

        foreach (var b in span)
        {
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }

        return (long)result;
    }

    private static string CreateLabel(ProtoNode node)
    {
        string payload = CreatePayload(node);

        return node.Children is { Count: > 0 }
            ? $"#{node.FieldNumber} [{node.WireType}] ← {node.Children.Count} 子节点"
            : $"#{node.FieldNumber} [{node.WireType}] → {payload}";
    }

    private static string CreateSummary(ProtoNode node)
    {
        if (node.Children is { Count: > 0 })
        {
            return $"嵌套类 · {node.Children.Count} 子节点 · 长度 {node.RawValue.Length}";
        }

        return node.WireType switch
        {
            ProtoWireType.Varint => $"{CreateVarintText(node.RawValue.Span)} · 长度 {node.RawValue.Length}",
            ProtoWireType.Fixed32 => $"0x{BitConverter.ToUInt32(node.RawValue.Span)} · 长度 {node.RawValue.Length}",
            ProtoWireType.Fixed64 => $"0x{BitConverter.ToUInt64(node.RawValue.Span)} · 长度 {node.RawValue.Length}",
            ProtoWireType.LengthDelimited => CreateLengthDelimitedSummary(node.RawValue.Span),
            _ => $"{node.RawValue.Length} bytes · 长度 {node.RawValue.Length}"
        };
    }

    private string FormatFieldDisplay()
    {
        string baseText = FieldNumber.ToString(CultureInfo.InvariantCulture);

        if (IsArrayGroup)
        {
            return $"[{this.Children.Count}]";
        }

        if (IsRepeated)
        {
            return $"{FieldNumber}[{OccurrenceIndex}]";
        }

        return baseText;
    }

    private static string CreatePayload(ProtoNode node)
    {
        return node.WireType switch
        {
            ProtoWireType.Varint => CreateVarintText(node.RawValue.Span),
            ProtoWireType.Fixed32 => $"0x{BitConverter.ToUInt32(node.RawValue.Span)} (LE)",
            ProtoWireType.Fixed64 => $"0x{BitConverter.ToUInt64(node.RawValue.Span)} (LE)",
            ProtoWireType.LengthDelimited => CreateLengthDelimitedText(node),
            _ => $"({node.RawValue.Length} bytes)"
        };
    }

    private static string CreateRawPreview(ProtoNode node)
    {
        if (node.RawValue.IsEmpty) return string.Empty;
        return BitConverter.ToString(node.RawValue.Span.ToArray()).Replace("-", " ");
    }

    private static string CreateLengthDelimitedSummary(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty) return "空";

        string? text = TryGetUtf8(span);
        if (!string.IsNullOrEmpty(text))
        {
            return $"UTF8 · \"{text}\" · 长度 {span.Length}";
        }

        if (span.Length <= 8)
        {
            return $"Bytes · {BitConverter.ToString(span.ToArray())} · 长度 {span.Length}";
        }

        return $"Bytes · 长度 {span.Length}";
    }

    private static string CreateVarintText(ReadOnlySpan<byte> span)
    {
        var value = VarintToValue(span);
        return $"{value} (0x{value:X})";
    }

    private static string CreateLengthDelimitedText(ProtoNode node)
    {
        if (node.Children is { Count: > 0 })
        {
            return $"嵌套类 ({node.Children.Count} 子节点) · 长度 {node.RawValue.Length}";
        }

        if (node.RawValue.IsEmpty)
        {
            return "长度 0";
        }

        string? utf8 = TryGetUtf8(node.RawValue.Span);
        string hex = BitConverter.ToString(node.RawValue.Span.ToArray());

        return utf8 is not null
            ? $"UTF8 \"{utf8}\" ({node.RawValue.Length} bytes) · 长度 {node.RawValue.Length}"
            : $"{node.RawValue.Length} bytes [{hex}] · 长度 {node.RawValue.Length}";
    }

    private static string? TryGetUtf8(ReadOnlySpan<byte> span)
    {
        try
        {
            var text = Encoding.UTF8.GetString(span);
            return text.Any(ch => char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t')
                ? null
                : text;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<ProtoDisplayNode> BuildDisplayNodes(IReadOnlyList<ProtoNode> nodes, string parentPath)
    {
        if (nodes.Count == 0)
        {
            return [];
        }

        var order = new List<int>();
        var grouped = new Dictionary<int, List<ProtoNode>>();

        foreach (var child in nodes)
        {
            if (!grouped.TryGetValue(child.FieldNumber, out var list))
            {
                list = [];
                grouped[child.FieldNumber] = list;
                order.Add(child.FieldNumber);
            }
            list.Add(child);
        }

        var result = new List<ProtoDisplayNode>();

        foreach (var fieldNumber in order)
        {
            var items = grouped[fieldNumber];
            var fieldSegment = fieldNumber.ToString(CultureInfo.InvariantCulture);
            var basePath = ComposePath(parentPath, fieldSegment);

            if (items.Count == 1)
            {
                result.Add(new ProtoDisplayNode(items[0], basePath));
                continue;
            }

            var children = new List<ProtoDisplayNode>(items.Count);
            var wireType = items[0].WireType;
            var totalLength = 0;

            for (var index = 0; index < items.Count; index++)
            {
                var occurrenceSegment = $"{fieldSegment}[{index + 1}]";
                var elementPath = ComposePath(parentPath, occurrenceSegment);
                children.Add(new ProtoDisplayNode(items[index], elementPath));
                totalLength += items[index].RawValue.Length;
            }

            result.Add(CreateArrayGroup(fieldNumber, wireType, basePath, children, totalLength));
        }

        return result;
    }

    private static ProtoDisplayNode CreateArrayGroup(int fieldNumber, ProtoWireType wireType, string path, IReadOnlyList<ProtoDisplayNode> children, int totalLength)
    {
        var label = $"#{fieldNumber} 数组";
        var summary = $"数组 · {children.Count} 个元素 · 长度 {totalLength}";
        return new ProtoDisplayNode(label, null, path, children, fieldNumber, wireType, summary, string.Empty);
    }

    private static string ComposePath(string parentPath, string segment)
        => string.IsNullOrEmpty(parentPath)
            ? segment
            : $"{parentPath}.{segment}";

    private static string GetLastSegment(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        var lastDot = path.LastIndexOf('.');
        return lastDot >= 0 ? path[(lastDot + 1)..] : path;
    }

    private static bool TryGetOccurrenceIndexFromSegment(string segment, out int index)
    {
        index = 1;
        if (string.IsNullOrEmpty(segment)) return false;

        var start = segment.IndexOf('[');
        if (start < 0) return false;

        var end = segment.IndexOf(']', start + 1);
        if (end < 0) return false;

        var numberSpan = segment.AsSpan(start + 1, end - start - 1);
        if (!int.TryParse(numberSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) 
            return false;
        
        index = value;
        return true;

    }
}


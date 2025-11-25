using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Avalonia.ProtoParse.Desktop.Core;

/// <summary>
/// Main parser for Protobuf binary data
/// </summary>
public static class ProtoParser
{
    /// <summary>
    /// Parse Protobuf binary data into a list of ProtoNode objects
    /// </summary>
    /// <param name="data">The raw Protobuf binary data</param>
    /// <returns>An enumerable of parsed ProtoNode objects</returns>
    public static IEnumerable<ProtoNode> Parse(ReadOnlyMemory<byte> data)
    {
        var reader = new ProtoReader(data);
        return ParseMessage(reader);
    }

    /// <summary>
    /// Pretty-print a list of ProtoNode objects for debugging
    /// </summary>
    /// <param name="nodes">The nodes to print</param>
    /// <param name="indent">Current indentation level</param>
    /// <returns>A formatted string representation</returns>
    public static string PrettyPrint(IEnumerable<ProtoNode> nodes, int indent = 0)
    {
        var sb = new StringBuilder();
        foreach (var node in nodes)
        {
            sb.Append(' ', indent * 2)
              .Append("Field ")
              .Append(node.FieldNumber)
              .Append(" (Wire=")
              .Append(node.WireType)
              .Append(") ");

            if (node.Children is { Count: > 0 })
            {
                sb.AppendLine("{")
                  .Append(PrettyPrint(node.Children, indent + 1))
                  .Append(' ', indent * 2)
                  .AppendLine("}");
            }
            else
            {
                sb.Append("=> ")
                  .AppendLine(Convert.ToHexString(node.RawValue.Span));
            }
        }
        return sb.ToString();
    }

    private static IEnumerable<ProtoNode> ParseMessage(ProtoReader reader)
    {
        while (!reader.IsAtEnd)
        {
            var key = reader.ReadVarint();
            var fieldNumber = (int)(key >> 3);
            var wireType = (ProtoWireType)(key & 0b111);

            switch (wireType)
            {
                case ProtoWireType.Varint:
                    yield return new ProtoNode
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        RawValue = reader.ReadVarintBytes(out _)
                    };
                    break;

                case ProtoWireType.Fixed32:
                    yield return new ProtoNode
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        RawValue = reader.ReadBytes(4)
                    };
                    break;

                case ProtoWireType.Fixed64:
                    yield return new ProtoNode
                    {
                        FieldNumber = fieldNumber,
                        WireType = wireType,
                        RawValue = reader.ReadBytes(8)
                    };
                    break;

                case ProtoWireType.LengthDelimited:
                {
                    var length = reader.ReadVarint();
                    if (length > int.MaxValue)
                        throw new InvalidOperationException("Length-delimited field too large.");

                    var payload = reader.ReadBytes((int)length);

                    // try parse recursively
                    if (!payload.IsEmpty && TryParseNested(payload, out var children))
                    {
                        yield return new ProtoNode
                        {
                            FieldNumber = fieldNumber,
                            WireType = wireType,
                            Children = children,
                            RawValue = payload
                        };
                    }
                    else
                    {
                        yield return new ProtoNode
                        {
                            FieldNumber = fieldNumber,
                            WireType = wireType,
                            RawValue = payload
                        };
                    }
                    break;
                }

                default:
                    throw new NotSupportedException($"Unsupported wire type: {wireType}");
            }
        }
    }

    private static bool TryParseNested(ReadOnlyMemory<byte> payload, out List<ProtoNode> nodes)
    {
        var nestedReader = new ProtoReader(payload);
        try
        {
            nodes = ParseMessage(nestedReader).ToList();
            return nestedReader.IsAtEnd && nestedReader.BytesRead == payload.Length;
        }
        catch
        {
            nodes = [];
            return false;
        }
    }

    /// <summary>
    /// Internal reader for parsing Protobuf binary format
    /// </summary>
    private class ProtoReader(ReadOnlyMemory<byte> data)
    {
        private int _position = 0;

        public bool IsAtEnd => _position >= data.Length;
        public int BytesRead => _position;

        public ReadOnlyMemory<byte> ReadBytes(int length)
        {
            var slice = data.Slice(_position, length);
            _position += length;
            return slice;
        }

        public ulong ReadVarint()
        {
            return ReadRawVarint64();
        }

        public ReadOnlyMemory<byte> ReadVarintBytes(out ulong value)
        {
            var start = _position;
            value = ReadRawVarint64();
            var consumed = _position - start;
            return data.Slice(start, consumed);
        }

        private ulong ReadRawVarint64()
        {
            ulong result = 0;
            var shift = 0;
            var span = data.Span;

            while (true)
            {
                if (_position >= span.Length)
                    throw new InvalidOperationException("Unexpected end of buffer while reading varint.");

                var b = span[_position++];
                result |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    break;

                shift += 7;
                if (shift >= 64)
                    throw new InvalidOperationException("Varint too long");
            }

            return result;
        }
    }
}

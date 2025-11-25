using System;
using System.Collections.Generic;

namespace Avalonia.ProtoParse.Desktop.Core;

/// <summary>
/// Represents a single Protobuf field node in the parsed structure
/// </summary>
public sealed class ProtoNode
{
    /// <summary>
    /// The field number from the Protobuf message
    /// </summary>
    public int FieldNumber { get; init; }
    
    /// <summary>
    /// The wire type of this field
    /// </summary>
    public ProtoWireType WireType { get; init; }
    
    /// <summary>
    /// The raw byte value of this field
    /// </summary>
    public ReadOnlyMemory<byte> RawValue { get; init; }
    
    /// <summary>
    /// Child nodes if this is a nested message (LengthDelimited)
    /// </summary>
    public List<ProtoNode>? Children { get; init; }

    /// <summary>
    /// Returns a string representation of this node
    /// </summary>
    public override string ToString()
        => Children is { Count: > 0 }
            ? $"Field {FieldNumber} ({WireType}) -> {Children.Count} child nodes"
            : $"Field {FieldNumber} ({WireType}) -> {RawValue.Length} bytes";
}


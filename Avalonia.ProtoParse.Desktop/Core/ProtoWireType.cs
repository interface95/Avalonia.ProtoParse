namespace Protobuf.Decode.Parser;

/// <summary>
/// Protobuf wire type enumeration
/// </summary>
public enum ProtoWireType
{
    /// <summary>
    /// Variable-length integer (int32, int64, uint32, uint64, sint32, sint64, bool, enum)
    /// </summary>
    Varint = 0,
    
    /// <summary>
    /// 64-bit fixed-length (fixed64, sfixed64, double)
    /// </summary>
    Fixed64 = 1,
    
    /// <summary>
    /// Length-delimited (string, bytes, embedded messages, packed repeated fields)
    /// </summary>
    LengthDelimited = 2,
    
    /// <summary>
    /// 32-bit fixed-length (fixed32, sfixed32, float)
    /// </summary>
    Fixed32 = 5
}


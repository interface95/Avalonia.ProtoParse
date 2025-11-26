using System.Text.Json.Serialization;

namespace Avalonia.ProtoParse.Desktop.Core;

[JsonSerializable(typeof(ProtoNodeDto))]
public partial class ProtoJsonContext : JsonSerializerContext
{
}

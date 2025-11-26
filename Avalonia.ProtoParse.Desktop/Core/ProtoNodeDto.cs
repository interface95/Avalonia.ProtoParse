using System.Collections.Generic;

namespace Avalonia.ProtoParse.Desktop.Core;

public record ProtoNodeDto(
    int Field,
    string WireType,
    string Path,
    string Summary,
    string? Value,
    List<ProtoNodeDto> Children);

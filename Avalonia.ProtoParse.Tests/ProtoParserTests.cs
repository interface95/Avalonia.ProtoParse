using System;
using System.Linq;
using Avalonia.ProtoParse.Desktop.Core;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.ProtoParse.Tests;

public class ProtoParserTests
{
    private readonly ITestOutputHelper _output;

    public ProtoParserTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Parse_ProvidedData_ShouldSucceed()
    {
        // Data provided by user
        const string base64Data = "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";

        var bytes = Convert.FromBase64String(base64Data);
        var nodes = ProtoParser.Parse(bytes).ToList();

        Assert.NotNull(nodes);
        Assert.NotEmpty(nodes);

        _output.WriteLine($"Parsed {nodes.Count} root nodes.");

        var prettyPrint = ProtoParser.PrettyPrint(nodes);
        _output.WriteLine(prettyPrint);

        // Basic validation based on structure
        Assert.Contains(nodes, n => n.FieldNumber == 1 && n.WireType == ProtoWireType.LengthDelimited);
        Assert.Contains(nodes, n => n.FieldNumber == 2 && n.WireType == ProtoWireType.LengthDelimited);
    }
}

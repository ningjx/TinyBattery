using System.IO;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Tests;

public sealed class DefinitionValidatorTests
{
    [Fact]
    public void LofreeDefinition_IsValid()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "lofree.flow-lite.json");
        var json = File.ReadAllText(path);

        var result = new DefinitionValidator().Validate(json);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void MissingProtocol_IsInvalid()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "example.device",
              "displayName": "Example",
              "match": { "transport": "hid", "vendorId": 1 }
            }
            """;

        var result = new DefinitionValidator().Validate(json);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void UnknownTransport_IsInvalid()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "id": "example.device",
              "displayName": "Example",
              "match": { "transport": "usb", "vendorId": 1 },
              "protocol": { "kind": "test", "options": {} }
            }
            """;

        var result = new DefinitionValidator().Validate(json);

        Assert.False(result.IsValid);
    }
}

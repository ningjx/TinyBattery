using System.Text.Json;
using System.Text.Json.Serialization;
using TinyBattery.Core.Importing;

namespace TinyBattery.Core.Serialization;

public static class DefinitionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new PackageItemManifestConverter(),
        },
    };
}

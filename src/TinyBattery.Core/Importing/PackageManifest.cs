using System.Text.Json;
using System.Text.Json.Serialization;

namespace TinyBattery.Core.Importing;

public sealed class PackageManifest
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public string? MinAppVersion { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    public List<PackageItemManifest>? Devices { get; init; }

    public List<PackageItemManifest>? Themes { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed class PackageItemManifest
{
    public string Path { get; init; } = "";

    public string? Id { get; init; }

    public string? Version { get; init; }
}

public sealed class PackageItemManifestConverter : JsonConverter<PackageItemManifest>
{
    public override PackageItemManifest Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new PackageItemManifest { Path = reader.GetString() ?? "" };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Package items must be a path string or an object.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new PackageItemManifest
        {
            Path = GetString(root, "path") ?? "",
            Id = GetString(root, "id"),
            Version = GetString(root, "version"),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        PackageItemManifest value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("path", value.Path);
        if (!string.IsNullOrWhiteSpace(value.Id))
        {
            writer.WriteString("id", value.Id);
        }

        if (!string.IsNullOrWhiteSpace(value.Version))
        {
            writer.WriteString("version", value.Version);
        }

        writer.WriteEndObject();
    }

    private static string? GetString(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }
}

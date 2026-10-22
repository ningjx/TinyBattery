using System.Text.Json;
using Json.Schema;
using TinyBattery.Abstractions.Plugins;

namespace TinyBattery.Infrastructure.Plugins;

public sealed class PluginManifestValidator
{
    private const string SchemaResourceName =
        "TinyBattery.Infrastructure.Schemas.plugin.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly JsonSchema _schema = Schema.Value;

    public bool TryLoad(
        string json,
        out PluginManifest? manifest,
        out IReadOnlyList<string> errors)
    {
        manifest = null;
        var errorList = new List<string>();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
        }
        catch (JsonException exception)
        {
            errors = [exception.Message];
            return false;
        }

        using (document)
        {
            var result = _schema.Evaluate(
                document.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!result.IsValid)
            {
                CollectErrors(result, errorList);
                if (errorList.Count == 0)
                {
                    errorList.Add("The plugin manifest does not match the schema.");
                }
            }
        }

        if (errorList.Count > 0)
        {
            errors = errorList;
            return false;
        }

        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            errors = [exception.Message];
            return false;
        }

        if (manifest is null)
        {
            errors = ["The plugin manifest produced an empty result."];
            return false;
        }

        if (manifest.SchemaVersion != 1)
        {
            errors = [$"Unsupported schemaVersion {manifest.SchemaVersion}; expected 1."];
            return false;
        }

        errors = [];
        return true;
    }

    private static JsonSchema LoadSchema()
    {
        var assembly = typeof(PluginManifestValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream(SchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema '{SchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static void CollectErrors(EvaluationResults result, List<string> errors)
    {
        if (result.IsValid)
        {
            return;
        }

        var details = result.Details;
        if (details is null || details.Count == 0)
        {
            errors.Add(result.InstanceLocation.ToString());
            return;
        }

        foreach (var detail in details)
        {
            if (!detail.IsValid)
            {
                CollectErrors(detail, errors);
            }
        }
    }
}

using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace TinyBattery.Core.Validation;

public static class JsonSchemaValidation
{
    public static JsonSchema LoadSchema(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    public static ValidationResult Validate(
        JsonSchema schema,
        string json,
        string fallbackError)
    {
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
            return new ValidationResult(false, [exception.Message]);
        }

        using (document)
        {
            var result = schema.Evaluate(
                document.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });

            if (result.IsValid)
            {
                return new ValidationResult(true, []);
            }

            var errors = new List<string>();
            CollectErrors(result, errors);
            if (errors.Count == 0)
            {
                errors.Add(fallbackError);
            }

            return new ValidationResult(false, errors);
        }
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

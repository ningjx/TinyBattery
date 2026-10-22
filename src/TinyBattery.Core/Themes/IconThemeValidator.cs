using Json.Schema;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Themes;

public sealed class IconThemeValidator
{
    private const string SchemaResourceName =
        "TinyBattery.Core.Schemas.icon-theme.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(
        () => JsonSchemaValidation.LoadSchema(
            typeof(IconThemeValidator).Assembly,
            SchemaResourceName));

    private readonly JsonSchema _schema = Schema.Value;

    public ValidationResult Validate(string json) =>
        JsonSchemaValidation.Validate(
            _schema,
            json,
            "The icon theme does not match the schema.");
}

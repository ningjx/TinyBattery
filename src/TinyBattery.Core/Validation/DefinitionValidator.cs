using Json.Schema;

namespace TinyBattery.Core.Validation;

public sealed class DefinitionValidator
{
    private const string SchemaResourceName =
        "TinyBattery.Core.Schemas.device-definition.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(
        () => JsonSchemaValidation.LoadSchema(
            typeof(DefinitionValidator).Assembly,
            SchemaResourceName));

    private readonly JsonSchema _schema = Schema.Value;

    public ValidationResult Validate(string json) =>
        JsonSchemaValidation.Validate(
            _schema,
            json,
            "The device definition does not match the schema.");
}

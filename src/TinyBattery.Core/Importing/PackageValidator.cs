using Json.Schema;
using TinyBattery.Core.Validation;

namespace TinyBattery.Core.Importing;

public sealed class PackageValidator
{
    private const string SchemaResourceName =
        "TinyBattery.Core.Schemas.package.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(
        () => JsonSchemaValidation.LoadSchema(
            typeof(PackageValidator).Assembly,
            SchemaResourceName));

    private readonly JsonSchema _schema = Schema.Value;

    public ValidationResult Validate(string json) =>
        JsonSchemaValidation.Validate(
            _schema,
            json,
            "The package manifest does not match the schema.");
}

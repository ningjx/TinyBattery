using System.Text.Json.Serialization;
using TinyBattery.Protocols.Engine;

namespace TinyBattery.Protocols.Options;

public sealed class RequestResponseOptions
{
    public int ReportId { get; init; }

    public bool IncludeReportId { get; init; } = true;

    public int TimeoutMs { get; init; } = 2000;

    public RequestSpec Request { get; init; } = new();

    public ResponseSpec Response { get; init; } = new();

    public Dictionary<string, FieldSpec> Extract { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RequestSpec
{
    public int[] Payload { get; init; } = [];

    public ChecksumSpec? Checksum { get; init; }
}

public sealed class ResponseSpec
{
    public List<MatchCondition> Match { get; init; } = [];

    public int MinLength { get; init; }
}

public sealed class MatchCondition
{
    public int Offset { get; init; }

    [JsonPropertyName("equals")]
    public int? EqualTo { get; init; }

    public int? Mask { get; init; }

    public int? Value { get; init; }
}

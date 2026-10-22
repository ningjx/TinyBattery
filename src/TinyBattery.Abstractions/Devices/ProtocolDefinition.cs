using System.Text.Json;

namespace TinyBattery.Abstractions.Devices;

public sealed record ProtocolDefinition(
    string Kind,
    JsonElement Options);

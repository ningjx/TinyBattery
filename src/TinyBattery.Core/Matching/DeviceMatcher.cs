using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Devices;
using TinyBattery.Abstractions.Registry;

namespace TinyBattery.Core.Matching;

public sealed class DeviceMatcher : IDeviceMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private readonly IDeviceDefinitionStore _store;
    private readonly ILogger<DeviceMatcher> _logger;

    public DeviceMatcher(
        IDeviceDefinitionStore store,
        ILogger<DeviceMatcher> logger)
    {
        _store = store;
        _logger = logger;
    }

    public MatchResult? Match(DeviceCandidate candidate)
    {
        MatchResult? best = null;

        foreach (var definition in _store.Definitions)
        {
            foreach (var match in definition.EffectiveMatches)
            {
                var result = TryMatch(candidate, definition, match);
                if (result is null)
                {
                    continue;
                }

                if (best is null
                    || result.Score > best.Score
                    || (result.Score == best.Score
                        && result.Definition.Priority > best.Definition.Priority))
                {
                    best = result;
                }
            }
        }

        if (best is not null)
        {
            _logger.LogDebug(
                "Device '{Device}' matched '{Definition}' with score {Score} ({Reasons}).",
                candidate.StableId,
                best.Definition.Id,
                best.Score,
                string.Join(", ", best.Reasons));
        }

        return best;
    }

    private static MatchResult? TryMatch(
        DeviceCandidate candidate,
        DeviceDefinition definition,
        DeviceMatch match)
    {
        if (candidate.Transport != match.Transport)
        {
            return null;
        }

        var score = 0;
        var reasons = new List<string>();

        if (!MatchesUInt16(candidate.VendorId, match.VendorId, "VID", ref score, reasons))
        {
            return null;
        }

        if (!MatchesUInt16(candidate.ProductId, match.ProductId, "PID", ref score, reasons))
        {
            return null;
        }

        if (!MatchesUInt16(candidate.UsagePage, match.UsagePage, "usage page", ref score, reasons))
        {
            return null;
        }

        if (!MatchesUInt16(candidate.Usage, match.Usage, "usage", ref score, reasons))
        {
            return null;
        }

        if (!MatchesString(candidate.Interface, match.Interface, "interface", 10, ref score, reasons))
        {
            return null;
        }

        if (!MatchesInt32(
                candidate.InputReportLength,
                match.InputReportLength,
                "input report length",
                5,
                ref score,
                reasons))
        {
            return null;
        }

        if (!MatchesInt32(
                candidate.OutputReportLength,
                match.OutputReportLength,
                "output report length",
                5,
                ref score,
                reasons))
        {
            return null;
        }

        if (!MatchesString(
                candidate.ServiceUuid,
                match.ServiceUuid,
                "service UUID",
                30,
                ref score,
                reasons))
        {
            return null;
        }

        if (!MatchesString(
                candidate.DeviceClass,
                match.DeviceClass,
                "device class",
                10,
                ref score,
                reasons))
        {
            return null;
        }

        if (!MatchesRegex(
                candidate.ProductName,
                match.ProductRegex,
                "product name",
                10,
                ref score,
                reasons))
        {
            return null;
        }

        if (!MatchesRegex(
                candidate.Manufacturer,
                match.ManufacturerRegex,
                "manufacturer",
                5,
                ref score,
                reasons))
        {
            return null;
        }

        if (!MatchesRegex(
                candidate.SerialNumber,
                match.SerialRegex,
                "serial number",
                10,
                ref score,
                reasons))
        {
            return null;
        }

        return new MatchResult(definition, score, reasons);
    }

    private static bool MatchesUInt16(
        ushort? actual,
        ushort? expected,
        string label,
        ref int score,
        List<string> reasons)
    {
        if (expected is null)
        {
            return true;
        }

        if (actual != expected)
        {
            return false;
        }

        score += label is "VID" or "PID" ? 20 : 15;
        reasons.Add($"{label} 0x{expected:X4}");
        return true;
    }

    private static bool MatchesInt32(
        int? actual,
        int? expected,
        string label,
        int weight,
        ref int score,
        List<string> reasons)
    {
        if (expected is null)
        {
            return true;
        }

        if (actual != expected)
        {
            return false;
        }

        score += weight;
        reasons.Add($"{label} {expected}");
        return true;
    }

    private static bool MatchesString(
        string? actual,
        string? expected,
        string label,
        int weight,
        ref int score,
        List<string> reasons)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return true;
        }

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        score += weight;
        reasons.Add($"{label} {expected}");
        return true;
    }

    private static bool MatchesRegex(
        string? actual,
        string? pattern,
        string label,
        int weight,
        ref int score,
        List<string> reasons)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        try
        {
            if (!Regex.IsMatch(
                    actual,
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    RegexTimeout))
            {
                return false;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        score += weight;
        reasons.Add($"{label} ~ /{pattern}/");
        return true;
    }
}

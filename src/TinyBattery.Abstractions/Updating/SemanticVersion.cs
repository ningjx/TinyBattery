using System.Globalization;

namespace TinyBattery.Abstractions.Updating;

public readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? Prerelease = null,
    string? BuildMetadata = null) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        string? metadata = null;
        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            metadata = text[(plus + 1)..];
            text = text[..plus];
        }

        string? prerelease = null;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = text[(dash + 1)..];
            text = text[..dash];
        }

        var parts = text.Split('.');
        if (parts.Length is < 1 or > 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major))
        {
            return false;
        }

        var minor = 0;
        var patch = 0;
        if (parts.Length > 1
            && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor))
        {
            return false;
        }

        if (parts.Length > 2
            && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease, metadata);
        return true;
    }

    public static SemanticVersion Parse(string value) =>
        TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a semantic version.");

    public int CompareTo(SemanticVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (string.IsNullOrWhiteSpace(Prerelease) && string.IsNullOrWhiteSpace(other.Prerelease))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(Prerelease))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(other.Prerelease))
        {
            return -1;
        }

        var left = Prerelease.Split('.');
        var right = other.Prerelease.Split('.');
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var leftNumeric = int.TryParse(left[index], out var leftNumber);
            var rightNumeric = int.TryParse(right[index], out var rightNumber);
            if (leftNumeric && rightNumeric)
            {
                result = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                result = -1;
            }
            else if (rightNumeric)
            {
                result = 1;
            }
            else
            {
                result = string.Compare(
                    left[index],
                    right[index],
                    StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}"
        + (string.IsNullOrWhiteSpace(Prerelease) ? string.Empty : $"-{Prerelease}");

    public static bool operator >(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) >= 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) =>
        left.CompareTo(right) <= 0;
}

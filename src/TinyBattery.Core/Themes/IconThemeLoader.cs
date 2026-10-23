using System.Xml;
using System.Xml.Linq;
using TinyBattery.Abstractions.Assets;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Core.Serialization;

namespace TinyBattery.Core.Themes;

public sealed class IconThemeLoader
{
    private static readonly string[] ImageExtensions = [".png", ".ico", ".jpg", ".jpeg"];
    private static readonly HashSet<string> SvgStateNames = new(
        ["full", "high", "mid", "low", "unknown", "charging"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IconThemeValidator _validator;
    private readonly IImageAssetValidator _imageValidator;

    public IconThemeLoader(
        IconThemeValidator validator,
        IImageAssetValidator imageValidator)
    {
        _validator = validator;
        _imageValidator = imageValidator;
    }

    public bool TryLoad(
        string manifestPath,
        out IconThemeDescriptor? descriptor,
        out IReadOnlyList<string> errors)
    {
        descriptor = null;
        var errorList = new List<string>();

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (IOException exception)
        {
            errors = [exception.Message];
            return false;
        }

        var validation = _validator.Validate(json);
        if (!validation.IsValid)
        {
            errors = validation.Errors;
            return false;
        }

        IconThemeManifest? manifest;
        try
        {
            manifest = System.Text.Json.JsonSerializer.Deserialize<IconThemeManifest>(
                json,
                DefinitionJson.Options);
        }
        catch (System.Text.Json.JsonException exception)
        {
            errors = [exception.Message];
            return false;
        }

        if (manifest is null)
        {
            errors = ["The theme manifest produced an empty result."];
            return false;
        }

        if (manifest.SchemaVersion != 1)
        {
            errors = [$"Unsupported schemaVersion {manifest.SchemaVersion}; expected 1."];
            return false;
        }

        var themeDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
            ?? throw new InvalidOperationException("Theme manifest has no directory.");

        var kind = manifest.Kind switch
        {
            "classic" => IconThemeKind.Classic,
            "imageSet" => IconThemeKind.ImageSet,
            "svg" => IconThemeKind.Svg,
            "xaml" => IconThemeKind.Xaml,
            "plugin" => IconThemeKind.Plugin,
            _ => (IconThemeKind?)null,
        };

        if (kind is null)
        {
            errors = [$"Unknown theme kind '{manifest.Kind}'."];
            return false;
        }

        if (kind is IconThemeKind.Xaml or IconThemeKind.Plugin)
        {
            errors = [$"Theme kind '{manifest.Kind}' is not supported by this build."];
            return false;
        }

        var preview = ResolveAsset(themeDirectory, manifest.Preview, "preview", errorList);
        var appIcon = ResolveAsset(themeDirectory, manifest.AppIcon, "appIcon", errorList);
        string? unknown = null;
        string? unknownSmall = null;
        string? chargingOverlay = null;
        if (kind != IconThemeKind.Svg)
        {
            unknown = ResolveAsset(themeDirectory, manifest.Unknown, "unknown", errorList);
            unknownSmall = ResolveAsset(
                themeDirectory,
                manifest.UnknownSmall,
                "unknownSmall",
                errorList);
            chargingOverlay = ResolveAsset(
                themeDirectory,
                manifest.ChargingOverlay,
                "chargingOverlay",
                errorList);
        }

        var levels = new List<IconThemeLevel>();
        ClassicIconThemeOptions? classic = null;
        SvgThemeOptions? svg = null;

        if (kind == IconThemeKind.Classic)
        {
            var source = manifest.Classic;
            classic = new ClassicIconThemeOptions(
                source?.HealthyThreshold ?? 60,
                source?.WarningThreshold ?? 20,
                source?.HealthyColor ?? "#45C96B",
                source?.WarningColor ?? "#D97706",
                source?.CriticalColor ?? "#C42B1C",
                source?.UnknownColor ?? "#7A7A7A",
                source?.UnknownFillRatio ?? 0.35);

            if (classic.WarningThreshold >= classic.HealthyThreshold)
            {
                errorList.Add("warningThreshold must be lower than healthyThreshold.");
            }

            if (classic.UnknownFillRatio is < 0 or > 1)
            {
                errorList.Add("unknownFillRatio must be between 0 and 1.");
            }
        }
        else if (kind == IconThemeKind.Svg)
        {
            svg = LoadSvgTheme(themeDirectory, manifest, errorList);
        }
        else
        {
            if (manifest.Levels is null || manifest.Levels.Count == 0)
            {
                errorList.Add("imageSet themes require at least one level.");
            }
            else
            {
                foreach (var level in manifest.Levels)
                {
                    var asset = ResolveAsset(
                        themeDirectory,
                        level.Asset,
                        $"level {level.MinPercent}%",
                        errorList);
                    var smallAsset = ResolveAsset(
                        themeDirectory,
                        level.SmallAsset,
                        $"level {level.MinPercent}% small",
                        errorList);
                    if (asset is not null)
                    {
                        levels.Add(new IconThemeLevel(
                            level.MinPercent,
                            asset,
                            smallAsset));
                    }
                }

                levels.Sort((left, right) => right.MinPercent.CompareTo(left.MinPercent));
            }
        }

        if (errorList.Count > 0)
        {
            errors = errorList;
            return false;
        }

        descriptor = new IconThemeDescriptor(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Author,
            manifest.Description,
            kind.Value,
            themeDirectory,
            preview,
            appIcon,
            classic,
            levels,
            unknown,
            unknownSmall,
            chargingOverlay,
            manifest.Metadata,
            svg);

        errors = [];
        return true;
    }

    private SvgThemeOptions? LoadSvgTheme(
        string themeDirectory,
        IconThemeManifest manifest,
        List<string> errors)
    {
        var render = manifest.Render;
        if (render is null)
        {
            errors.Add("SVG themes require a render object.");
            return null;
        }

        if (render.Slots is null || render.Slots.Count == 0)
        {
            errors.Add("SVG themes require at least one slot.");
            return null;
        }

        var declaredCount = manifest.SlotCount ?? render.Slots.Count;
        if (declaredCount != render.Slots.Count)
        {
            errors.Add(
                $"slotCount is {declaredCount} but {render.Slots.Count} slots are defined.");
        }

        if (declaredCount < 1 || declaredCount > 8)
        {
            errors.Add("slotCount must be between 1 and 8.");
        }

        var slots = new List<IconThemeSlot>();
        var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in render.Slots)
        {
            if (!slotIds.Add(slot.Id))
            {
                errors.Add($"Duplicate slot id '{slot.Id}'.");
                continue;
            }

            if (!Enum.TryParse<IconFillMode>(slot.Mode, ignoreCase: true, out var mode))
            {
                errors.Add($"Unknown fill mode '{slot.Mode}' for slot '{slot.Id}'.");
                continue;
            }

            if (mode == IconFillMode.Tint && declaredCount != 1)
            {
                errors.Add("tint mode is only supported for single-slot themes.");
            }

            slots.Add(new IconThemeSlot(slot.Id, slot.Label, mode, slot.Target));
        }

        var svgPath = ResolveSvg(themeDirectory, render.Svg, "render.svg", errors);
        XDocument? svgDocument = null;
        if (svgPath is not null)
        {
            svgDocument = LoadSvgDocument(svgPath, "render.svg", errors);
            if (svgDocument is not null)
            {
                ValidateSvgTargets(svgDocument, slots, render.Svg, errors);
            }
        }

        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (state, relativePath) in render.States ?? new Dictionary<string, string>())
        {
            if (!SvgStateNames.Contains(state))
            {
                errors.Add($"Unknown SVG state '{state}'.");
                continue;
            }

            var path = ResolveSvg(themeDirectory, relativePath, $"state {state}", errors);
            if (path is not null)
            {
                LoadSvgDocument(path, $"state {state}", errors);
                if (!string.Equals(state, "charging", StringComparison.OrdinalIgnoreCase))
                {
                    states[state] = path;
                }
            }
        }

        string? charging = null;
        var configuredCharging = render.States?
            .FirstOrDefault(pair => string.Equals(
                pair.Key,
                "charging",
                StringComparison.OrdinalIgnoreCase))
            .Value;
        var chargingPath = !string.IsNullOrWhiteSpace(configuredCharging)
            ? configuredCharging
            : manifest.ChargingOverlay;
        if (!string.IsNullOrWhiteSpace(chargingPath))
        {
            var path = ResolveSvg(themeDirectory, chargingPath, "charging overlay", errors);
            if (path is not null)
            {
                LoadSvgDocument(path, "charging overlay", errors);
                charging = path;
            }
        }

        if (svgPath is null || errorListHasErrors(errors))
        {
            return null;
        }

        var colors = render.Colors is null
            ? new ThemeColorSet()
            : new ThemeColorSet(
                render.Colors.Healthy,
                render.Colors.Warning,
                render.Colors.Critical,
                render.Colors.Unknown,
                render.Colors.Track);

        return new SvgThemeOptions(
            svgPath,
            slots,
            colors,
            charging,
            states,
            render.StartAngle ?? -90,
            render.Clockwise ?? true,
            render.InnerRadius ?? 0);
    }

    private static bool errorListHasErrors(List<string> errors) => errors.Count > 0;

    private static void ValidateSvgTargets(
        XDocument document,
        IReadOnlyList<IconThemeSlot> slots,
        string svgLabel,
        List<string> errors)
    {
        foreach (var slot in slots)
        {
            if (slot.Mode == IconFillMode.Tint)
            {
                continue;
            }

            var element = document
                .Descendants()
                .FirstOrDefault(candidate => string.Equals(
                    (string?)candidate.Attribute("id"),
                    slot.Target,
                    StringComparison.OrdinalIgnoreCase));
            if (element is null)
            {
                errors.Add($"Slot '{slot.Id}' target '{slot.Target}' was not found in {svgLabel}.");
                continue;
            }

            var name = element.Name.LocalName;
            var expected = slot.Mode is IconFillMode.Ring or IconFillMode.Sector
                ? "circle"
                : "rect";
            if (!string.Equals(name, expected, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Slot '{slot.Id}' target '{slot.Target}' must be a <{expected}> for "
                    + $"{slot.Mode} mode.");
            }

            if (!string.IsNullOrWhiteSpace((string?)element.Attribute("transform")))
            {
                errors.Add(
                    $"Slot '{slot.Id}' target '{slot.Target}' must not use a transform.");
            }
        }
    }

    private string? ResolveSvg(
        string themeDirectory,
        string? relativePath,
        string label,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            errors.Add($"{label} path is empty.");
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(themeDirectory, relativePath));
        if (!IsUnder(themeDirectory, fullPath))
        {
            errors.Add($"{label} escapes the theme directory: {relativePath}");
            return null;
        }

        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} was not found: {relativePath}");
            return null;
        }

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".svg",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} must be an .svg file: {relativePath}");
            return null;
        }

        if (new FileInfo(fullPath).Length > 256 * 1024)
        {
            errors.Add($"{label} exceeds 256 KB: {relativePath}");
            return null;
        }

        return fullPath;
    }

    private static XDocument? LoadSvgDocument(
        string path,
        string label,
        List<string> errors)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                });
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root is null
                || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} is not an SVG document.");
                return null;
            }

            if (root.Attribute("viewBox") is null
                && (root.Attribute("width") is null || root.Attribute("height") is null))
            {
                errors.Add($"{label} must define a viewBox or width/height.");
            }

            foreach (var element in document.Descendants())
            {
                var name = element.Name.LocalName;
                if (name is "script" or "foreignObject" or "image")
                {
                    errors.Add($"{label} contains a disallowed <{name}> element.");
                }

                foreach (var attribute in element.Attributes())
                {
                    if (attribute.Name.LocalName is "href"
                        && !attribute.Value.StartsWith('#'))
                    {
                        errors.Add($"{label} contains an external reference.");
                    }
                }
            }

            return document;
        }
        catch (Exception exception) when (
            exception is XmlException or IOException or UnauthorizedAccessException)
        {
            errors.Add($"{label} could not be parsed: {exception.Message}");
            return null;
        }
    }

    private string? ResolveAsset(
        string themeDirectory,
        string? relativePath,
        string label,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(themeDirectory, relativePath));
        if (!IsUnder(themeDirectory, fullPath))
        {
            errors.Add($"{label} escapes the theme directory: {relativePath}");
            return null;
        }

        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} was not found: {relativePath}");
            return null;
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension, StringComparer.Ordinal))
        {
            errors.Add($"{label} has unsupported image extension '{extension}'.");
            return null;
        }

        if (new FileInfo(fullPath).Length > 10 * 1024 * 1024)
        {
            errors.Add($"{label} exceeds 10 MB: {relativePath}");
            return null;
        }

        if (!_imageValidator.TryValidate(fullPath, out var imageError))
        {
            errors.Add($"{label}: {imageError}");
            return null;
        }

        return fullPath;
    }

    private static bool IsUnder(string root, string fullPath)
    {
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
    }
}

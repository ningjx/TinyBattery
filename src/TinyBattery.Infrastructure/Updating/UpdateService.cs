using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Packages;
using TinyBattery.Abstractions.Paths;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Updating;

namespace TinyBattery.Infrastructure.Updating;

public sealed class UpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IAppPaths _paths;
    private readonly IUserSettingsStore _settings;
    private readonly IInstalledPackageStore _installed;
    private readonly IPackageImporter _packages;
    private readonly HttpClient _http;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IAppPaths paths,
        IUserSettingsStore settings,
        IInstalledPackageStore installed,
        IPackageImporter packages,
        HttpClient http,
        ILogger<UpdateService> logger)
    {
        _paths = paths;
        _settings = settings;
        _installed = installed;
        _packages = packages;
        _http = http;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct)
    {
        var feedUrl = _settings.Current.UpdateFeedUrl;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return new UpdateCheckResult(
                false,
                "尚未配置更新源。请在 settings.json 中设置 updateFeedUrl。",
                []);
        }

        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new UpdateCheckResult(false, "更新源必须是 HTTPS 地址。", []);
        }

        try
        {
            using var response = await _http.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 1024 * 1024)
            {
                return new UpdateCheckResult(false, "更新源响应过大。", []);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            if (json.Length > 1024 * 1024)
            {
                return new UpdateCheckResult(false, "更新源响应过大。", []);
            }

            var feed = JsonSerializer.Deserialize<UpdateFeed>(json, JsonOptions);
            if (feed is null || feed.SchemaVersion != 1)
            {
                return new UpdateCheckResult(false, "更新源格式不受支持。", []);
            }

            var installed = await _installed.LoadAsync(ct);
            var installedById = installed.ToDictionary(
                package => package.Id,
                StringComparer.OrdinalIgnoreCase);
            var appVersion = GetAppVersion();
            var updates = new List<AvailableUpdate>();
            foreach (var package in feed.Packages)
            {
                if (!SemanticVersion.TryParse(package.Version, out var feedVersion))
                {
                    _logger.LogWarning(
                        "Update feed package '{Id}' has invalid version '{Version}'.",
                        package.Id,
                        package.Version);
                    continue;
                }

                installedById.TryGetValue(package.Id, out var current);
                var currentVersion = current is not null
                    && SemanticVersion.TryParse(current.Version, out var parsedCurrent)
                        ? parsedCurrent
                        : new SemanticVersion(0, 0, 0);
                var changes = BuildChanges(package, current);
                if (feedVersion <= currentVersion && changes.Count == 0)
                {
                    continue;
                }

                var compatible = true;
                if (!string.IsNullOrWhiteSpace(package.MinAppVersion))
                {
                    if (!SemanticVersion.TryParse(package.MinAppVersion, out var minimum))
                    {
                        compatible = false;
                    }
                    else
                    {
                        compatible = appVersion >= minimum;
                    }
                }

                updates.Add(new AvailableUpdate(
                    package,
                    currentVersion,
                    compatible,
                    changes));
            }

            updates = updates
                .OrderByDescending(update => update.Package.Version, SemVerComparer.Instance)
                .ToList();

            return updates.Count == 0
                ? new UpdateCheckResult(true, "已经是最新版本。", [])
                : new UpdateCheckResult(
                    true,
                    $"发现 {updates.Count} 个可用更新。",
                    updates);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or IOException
                or JsonException
                or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Update check failed.");
            return new UpdateCheckResult(false, $"检查更新失败：{exception.Message}", []);
        }
    }

    public async Task<UpdateInstallResult> InstallAsync(
        AvailableUpdate update,
        CancellationToken ct)
    {
        if (!update.IsCompatible)
        {
            return new UpdateInstallResult(
                false,
                "该更新要求更高版本的 TinyBattery。",
                []);
        }

        if (!Uri.TryCreate(update.Package.Url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new UpdateInstallResult(false, "更新包地址必须是 HTTPS。", []);
        }

        Directory.CreateDirectory(_paths.UpdateCacheDirectory);
        var target = Path.Combine(
            _paths.UpdateCacheDirectory,
            $"{SafeName(update.Package.Id)}-{SafeName(update.Package.Version)}.tbpack");

        try
        {
            using (var response = await _http.GetAsync(
                       uri,
                       HttpCompletionOption.ResponseHeadersRead,
                       ct))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 50L * 1024 * 1024)
                {
                    return new UpdateInstallResult(false, "更新包过大。", []);
                }

                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = File.Create(target);
                await input.CopyToAsync(output, ct);
            }

            var length = new FileInfo(target).Length;
            if (update.Package.Size is long expectedSize && length != expectedSize)
            {
                return new UpdateInstallResult(false, "更新包大小与更新源不一致。", []);
            }

            if (!string.IsNullOrWhiteSpace(update.Package.Sha256))
            {
                var hash = await ComputeSha256Async(target, ct);
                if (!string.Equals(
                        hash,
                        update.Package.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new UpdateInstallResult(false, "更新包哈希校验失败。", []);
                }
            }

            var result = await _packages.ImportAsync(
                target,
                new ImportOptions(Overwrite: true, Source: update.Package.Url),
                ct);
            if (!result.Success)
            {
                return new UpdateInstallResult(
                    false,
                    string.Join("; ", result.Diagnostics),
                    result.InstalledPaths);
            }

            _logger.LogInformation(
                "Installed update package '{Id}' version '{Version}'.",
                update.Package.Id,
                update.Package.Version);
            return new UpdateInstallResult(
                true,
                $"已更新到 {update.Package.Version}。",
                result.InstalledPaths);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or IOException
                or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Update installation failed.");
            return new UpdateInstallResult(
                false,
                $"安装更新失败：{exception.Message}",
                []);
        }
        finally
        {
            if (File.Exists(target))
            {
                try
                {
                    File.Delete(target);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static List<UpdateItemChange> BuildChanges(
        UpdatePackageEntry package,
        InstalledPackage? current)
    {
        var changes = new List<UpdateItemChange>();
        AddChanges("device", package.Items.Devices, current, changes);
        AddChanges("theme", package.Items.Themes, current, changes);
        return changes;
    }

    private static void AddChanges(
        string kind,
        IReadOnlyList<UpdateItemEntry> items,
        InstalledPackage? current,
        List<UpdateItemChange> changes)
    {
        foreach (var item in items)
        {
            var installed = current?.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            var currentVersion = installed?.Version;
            var newer = !SemanticVersion.TryParse(currentVersion, out var installedVersion)
                || !SemanticVersion.TryParse(item.Version, out var newVersion)
                || newVersion > installedVersion;
            if (newer)
            {
                changes.Add(new UpdateItemChange(
                    kind,
                    item.Id,
                    currentVersion,
                    item.Version));
            }
        }
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    private static SemanticVersion GetAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null
            ? new SemanticVersion(0, 0, 0)
            : new SemanticVersion(
                Math.Max(0, version.Major),
                Math.Max(0, version.Minor),
                Math.Max(0, version.Build));
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray());
    }

    private sealed class SemVerComparer : IComparer<string>
    {
        public static readonly SemVerComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var left = SemanticVersion.TryParse(x, out var l) ? l : new SemanticVersion(0, 0, 0);
            var right = SemanticVersion.TryParse(y, out var r) ? r : new SemanticVersion(0, 0, 0);
            return left.CompareTo(right);
        }
    }
}

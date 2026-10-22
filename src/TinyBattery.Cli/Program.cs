using Microsoft.Extensions.Logging.Abstractions;
using TinyBattery.Abstractions.Importing;
using TinyBattery.Abstractions.Protocols;
using TinyBattery.Abstractions.Settings;
using TinyBattery.Abstractions.Themes;
using TinyBattery.Core.Importing;
using TinyBattery.Core.Matching;
using TinyBattery.Core.Registry;
using TinyBattery.Core.Serialization;
using TinyBattery.Core.Themes;
using TinyBattery.Core.Validation;
using TinyBattery.Infrastructure.Assets;
using TinyBattery.Infrastructure.Hid;
using TinyBattery.Infrastructure.Paths;
using TinyBattery.Infrastructure.Plugins;
using TinyBattery.Infrastructure.Settings;
using TinyBattery.Protocols;
using TinyBattery.Protocols.Hid;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    if (args.Length == 0)
    {
        PrintUsage();
        return 1;
    }

    return args[0].ToLowerInvariant() switch
    {
        "validate" => await ValidateDeviceAsync(args),
        "validate-theme" => await ValidateThemeAsync(args),
        "validate-package" => await ValidatePackageAsync(args),
        "definitions" => await ListDefinitionsAsync(),
        "themes" => await ListThemesAsync(),
        "plugins" => await ListPluginsAsync(),
        "hid" => await ListHidAsync(),
        "probe" => await ProbeAsync(),
        "select-theme" => await SelectThemeAsync(args),
        "import-device" => await ImportDeviceAsync(args),
        "import-theme" => await ImportThemeAsync(args),
        "import-package" => await ImportPackageAsync(args),
        "paths" => PrintPaths(),
        _ => UnknownCommand(args[0]),
    };
}

static Task<int> ValidateDeviceAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: TinyBattery.Cli validate <device-definition.json>");
        return Task.FromResult(1);
    }

    var path = Path.GetFullPath(args[1]);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"文件不存在: {path}");
        return Task.FromResult(1);
    }

    var json = File.ReadAllText(path);
    var result = new DefinitionValidator().Validate(json);
    if (!result.IsValid)
    {
        PrintErrors(path, result.Errors);
        return Task.FromResult(1);
    }

    try
    {
        var definition = System.Text.Json.JsonSerializer.Deserialize<
            TinyBattery.Abstractions.Devices.DeviceDefinition>(
            json,
            DefinitionJson.Options);
        Console.WriteLine($"✓ {path}");
        if (definition is not null)
        {
            Console.WriteLine($"  id: {definition.Id}");
            Console.WriteLine($"  name: {definition.DisplayName}");
            Console.WriteLine($"  protocol: {definition.Protocol.Kind}");
        }
    }
    catch (System.Text.Json.JsonException exception)
    {
        Console.Error.WriteLine($"✗ 反序列化失败: {exception.Message}");
        return Task.FromResult(1);
    }

    return Task.FromResult(0);
}

static Task<int> ValidateThemeAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: TinyBattery.Cli validate-theme <theme.json|主题文件夹>");
        return Task.FromResult(1);
    }

    var path = Path.GetFullPath(args[1]);
    var manifest = Directory.Exists(path)
        ? Path.Combine(path, "theme.json")
        : path;

    var loader = new IconThemeLoader(
        new IconThemeValidator(),
        new SystemDrawingImageAssetValidator());
    if (!loader.TryLoad(manifest, out var theme, out var errors) || theme is null)
    {
        PrintErrors(manifest, errors);
        return Task.FromResult(1);
    }

    Console.WriteLine($"✓ {manifest}");
    Console.WriteLine($"  id: {theme.Id}");
    Console.WriteLine($"  name: {theme.DisplayName}");
    Console.WriteLine($"  kind: {theme.Kind}");
    Console.WriteLine($"  levels: {theme.Levels?.Count ?? 0}");
    return Task.FromResult(0);
}

static Task<int> ValidatePackageAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: TinyBattery.Cli validate-package <package.json>");
        return Task.FromResult(1);
    }

    var path = Path.GetFullPath(args[1]);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"文件不存在: {path}");
        return Task.FromResult(1);
    }

    var result = new PackageValidator().Validate(File.ReadAllText(path));
    if (!result.IsValid)
    {
        PrintErrors(path, result.Errors);
        return Task.FromResult(1);
    }

    Console.WriteLine($"✓ {path}");
    return Task.FromResult(0);
}

static async Task<int> ListDefinitionsAsync()
{
    var paths = new AppPaths();
    var (catalog, _) = await LoadPluginsAsync(paths);
    var store = new JsonDeviceDefinitionStore(
        paths,
        catalog,
        new DefinitionValidator(),
        NullLogger<JsonDeviceDefinitionStore>.Instance);

    await store.ReloadAsync(CancellationToken.None);

    Console.WriteLine($"已加载 {store.Definitions.Count} 个设备定义:");
    foreach (var definition in store.Definitions)
    {
        Console.WriteLine(
            $"  {definition.DisplayName,-28} {definition.Id,-32} {definition.Protocol.Kind}");
    }

    PrintDefinitionDiagnostics(store);
    return 0;
}

static async Task<int> ListThemesAsync()
{
    var paths = new AppPaths();
    var (catalog, _) = await LoadPluginsAsync(paths);
    var settings = await LoadSettingsAsync(paths);
    var store = CreateThemeStore(paths, catalog, settings);

    await store.ReloadAsync(CancellationToken.None);

    Console.WriteLine($"已加载 {store.Themes.Count} 个图标主题，当前: {store.Selected.Id}");
    foreach (var theme in store.Themes)
    {
        var marker = string.Equals(theme.Id, store.Selected.Id, StringComparison.OrdinalIgnoreCase)
            ? "*"
            : " ";
        Console.WriteLine(
            $" {marker} {theme.DisplayName,-28} {theme.Id,-32} {theme.Kind}");
    }

    if (store.Diagnostics.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"诊断 {store.Diagnostics.Count} 条:");
        foreach (var diagnostic in store.Diagnostics)
        {
            Console.WriteLine($"  {diagnostic.Source}");
            foreach (var error in diagnostic.Errors)
            {
                Console.WriteLine($"    - {error}");
            }
        }
    }

    return 0;
}

static async Task<int> SelectThemeAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("用法: TinyBattery.Cli select-theme <theme-id>");
        return 1;
    }

    var paths = new AppPaths();
    var (catalog, _) = await LoadPluginsAsync(paths);
    var settings = await LoadSettingsAsync(paths);
    var store = CreateThemeStore(paths, catalog, settings);
    await store.ReloadAsync(CancellationToken.None);

    try
    {
        await store.SelectAsync(args[1], CancellationToken.None);
        Console.WriteLine($"已选择主题: {args[1]}");
        return 0;
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static async Task<int> ImportDeviceAsync(string[] args) =>
    await ImportAsync(
        args,
        "import-device <device.json|文件夹> [--overwrite]",
        (paths, registry) => new DeviceDefinitionImporter(
            paths,
            registry,
            new DefinitionValidator(),
            NullLogger<DeviceDefinitionImporter>.Instance));

static async Task<int> ImportThemeAsync(string[] args) =>
    await ImportAsync(
        args,
        "import-theme <theme.json|文件夹|.tbtheme|.zip> [--overwrite]",
        (paths, _) => new IconThemeImporter(
            paths,
            new IconThemeLoader(
                new IconThemeValidator(),
                new SystemDrawingImageAssetValidator()),
            new SafeZipExtractor(),
            NullLogger<IconThemeImporter>.Instance));

static async Task<int> ImportPackageAsync(string[] args) =>
    await ImportAsync(
        args,
        "import-package <package.json|文件夹|.tbpack|.zip> [--overwrite]",
        (paths, registry) =>
        {
            var deviceImporter = new DeviceDefinitionImporter(
                paths,
                registry,
                new DefinitionValidator(),
                NullLogger<DeviceDefinitionImporter>.Instance);
            var themeImporter = new IconThemeImporter(
                paths,
                new IconThemeLoader(
                    new IconThemeValidator(),
                    new SystemDrawingImageAssetValidator()),
                new SafeZipExtractor(),
                NullLogger<IconThemeImporter>.Instance);
            return new PackageImporter(
                deviceImporter,
                themeImporter,
                new PackageValidator(),
                new SafeZipExtractor(),
                NullLogger<PackageImporter>.Instance);
        });

static async Task<int> ImportAsync<T>(
    string[] args,
    string usage,
    Func<AppPaths, ProtocolRegistry, T> createImporter)
    where T : IImporter
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine($"用法: TinyBattery.Cli {usage}");
        return 1;
    }

    var source = Path.GetFullPath(args[1]);
    var paths = new AppPaths();
    paths.EnsureCreated();

    var (_, registry) = await LoadPluginsAsync(paths);
    var importer = createImporter(paths, registry);
    var result = await importer.ImportAsync(
        source,
        new ImportOptions(HasOverwrite(args)),
        CancellationToken.None);

    return PrintImportResult(result);
}

static int PrintImportResult(ImportResult result)
{
    if (result.Success)
    {
        Console.WriteLine("✓ 导入成功:");
        foreach (var path in result.InstalledPaths)
        {
            Console.WriteLine($"  {path}");
        }
    }
    else
    {
        Console.WriteLine("✗ 导入失败:");
    }

    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine($"  - {diagnostic}");
    }

    return result.Success ? 0 : 1;
}

static bool HasOverwrite(string[] args) =>
    args.Any(arg => string.Equals(arg, "--overwrite", StringComparison.OrdinalIgnoreCase));

static async Task<JsonUserSettingsStore> LoadSettingsAsync(AppPaths paths)
{
    var settings = new JsonUserSettingsStore(
        paths,
        NullLogger<JsonUserSettingsStore>.Instance);
    await settings.LoadAsync(CancellationToken.None);
    return settings;
}

static JsonIconThemeStore CreateThemeStore(
    AppPaths paths,
    PluginAssetCatalog catalog,
    IUserSettingsStore settings) =>
    new(
        paths,
        catalog,
        settings,
        new IconThemeLoader(
            new IconThemeValidator(),
            new SystemDrawingImageAssetValidator()),
        NullLogger<JsonIconThemeStore>.Instance);

static async Task<(PluginAssetCatalog Catalog, ProtocolRegistry Registry)> LoadPluginsAsync(
    AppPaths paths)
{
    var catalog = new PluginAssetCatalog();
    var registry = new ProtocolRegistry(
        [new RequestResponseProtocol(NullLogger<RequestResponseProtocol>.Instance)]);
    var loader = new PluginLoader(
        paths,
        catalog,
        registry,
        new PluginManifestValidator(),
        NullLoggerFactory.Instance,
        NullLogger<PluginLoader>.Instance);
    await loader.LoadAsync(CancellationToken.None);
    return (catalog, registry);
}

static async Task<int> ListPluginsAsync()
{
    var paths = new AppPaths();
    var (catalog, registry) = await LoadPluginsAsync(paths);

    Console.WriteLine($"插件协议 {catalog.Protocols.Count} 个:");
    foreach (var protocol in catalog.Protocols)
    {
        Console.WriteLine($"  {protocol.Kind}");
    }

    Console.WriteLine($"插件主题 {catalog.ThemePlugins.Count} 个:");
    foreach (var theme in catalog.ThemePlugins)
    {
        Console.WriteLine($"  {theme.Describe().Id}");
    }

    Console.WriteLine($"设备定义文件 {catalog.DeviceDefinitionPaths.Count} 个:");
    foreach (var path in catalog.DeviceDefinitionPaths)
    {
        Console.WriteLine($"  {path}");
    }

    Console.WriteLine($"主题清单 {catalog.ThemeManifestPaths.Count} 个:");
    foreach (var path in catalog.ThemeManifestPaths)
    {
        Console.WriteLine($"  {path}");
    }

    Console.WriteLine($"协议注册表可解析: hid.request-response={registry.Resolve("hid.request-response").Kind}");
    foreach (var protocol in catalog.Protocols)
    {
        Console.WriteLine($"  可解析 {protocol.Kind}");
    }

    if (catalog.Diagnostics.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"插件诊断 {catalog.Diagnostics.Count} 条:");
        foreach (var diagnostic in catalog.Diagnostics)
        {
            Console.WriteLine($"  {diagnostic.Source}: {diagnostic.Message}");
        }
    }

    return 0;
}

static void PrintDefinitionDiagnostics(JsonDeviceDefinitionStore store)
{
    if (store.Diagnostics.Count == 0)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"诊断 {store.Diagnostics.Count} 条:");
    foreach (var diagnostic in store.Diagnostics)
    {
        Console.WriteLine($"  {diagnostic.Source}");
        foreach (var error in diagnostic.Errors)
        {
            Console.WriteLine($"    - {error}");
        }
    }
}

static void PrintErrors(string source, IReadOnlyList<string> errors)
{
    Console.Error.WriteLine($"✗ {source}");
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }
}

static async Task<int> ListHidAsync()
{
    var paths = new AppPaths();
    var (_, _, store) = await LoadRuntimeAsync(paths);
    var matcher = new DeviceMatcher(store, NullLogger<DeviceMatcher>.Instance);
    var transport = new HidSharpTransport();
    var candidates = await transport.DiscoverAsync(CancellationToken.None);

    Console.WriteLine($"HID 接口 {candidates.Count} 个:");
    foreach (var candidate in candidates
                 .OrderBy(item => item.VendorId)
                 .ThenBy(item => item.ProductId)
                 .ThenBy(item => item.Interface))
    {
        var match = matcher.Match(candidate);
        Console.WriteLine(
            $"  {candidate.VendorId:X4}:{candidate.ProductId:X4} "
            + $"UsagePage=0x{candidate.UsagePage:X4} Usage=0x{candidate.Usage:X4} "
            + $"If={candidate.Interface ?? "--"} In={candidate.InputReportLength} Out={candidate.OutputReportLength}");
        Console.WriteLine($"    {candidate.ProductName ?? "(no name)"}  -> {match?.Definition.Id ?? "(no definition)"}");
    }

    return 0;
}

static async Task<int> ProbeAsync()
{
    Console.WriteLine("只读探测：只发送 HID++ 特征发现和电量查询，不写入任何键盘设置。");
    var paths = new AppPaths();
    var (_, registry, store) = await LoadRuntimeAsync(paths);
    var matcher = new DeviceMatcher(store, NullLogger<DeviceMatcher>.Instance);
    var transport = new HidSharpTransport();
    var candidates = await transport.DiscoverAsync(CancellationToken.None);
    var found = 0;

    foreach (var candidate in candidates)
    {
        var match = matcher.Match(candidate);
        if (match is null)
        {
            continue;
        }

        found++;
        Console.WriteLine(
            $"设备: {match.Definition.DisplayName} "
            + $"({candidate.VendorId:X4}:{candidate.ProductId:X4}, {candidate.Interface})");

        try
        {
            await using var connection = await transport.OpenAsync(
                candidate,
                CancellationToken.None);
            var protocol = registry.Resolve(match.Definition.Protocol.Kind);
            var readings = await protocol.ReadAsync(
                new ProtocolContext(candidate, connection, match.Definition),
                CancellationToken.None);

            foreach (var reading in readings)
            {
                var battery = reading.Battery;
                Console.WriteLine(
                    $"  {reading.DisplayName}: "
                    + $"Percent={battery.Percent?.ToString() ?? "--"} "
                    + $"VoltageMv={battery.VoltageMv?.ToString("0") ?? "--"} "
                    + $"Charging={battery.Charging}");
                if (battery.RawHex is { } raw)
                {
                    Console.WriteLine($"  Raw={raw}");
                }
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"  读取失败: {exception.Message}");
        }
    }

    if (found == 0)
    {
        Console.WriteLine("没有 HID 接口匹配已加载的设备定义。先运行 `hid` 查看接口列表。");
        return 1;
    }

    return 0;
}

static async Task<(PluginAssetCatalog Catalog, ProtocolRegistry Registry, JsonDeviceDefinitionStore Store)> LoadRuntimeAsync(
    AppPaths paths)
{
    var (catalog, registry) = await LoadPluginsAsync(paths);
    var store = new JsonDeviceDefinitionStore(
        paths,
        catalog,
        new DefinitionValidator(),
        NullLogger<JsonDeviceDefinitionStore>.Instance);
    await store.ReloadAsync(CancellationToken.None);
    return (catalog, registry, store);
}

static int PrintPaths()
{
    var paths = new AppPaths();
    Console.WriteLine($"root:        {paths.RootDirectory}");
    Console.WriteLine($"definitions: {paths.DefinitionsDirectory}");
    Console.WriteLine($"themes:      {paths.ThemesDirectory}");
    Console.WriteLine($"plugins:     {paths.PluginsDirectory}");
    Console.WriteLine($"logs:        {paths.LogsDirectory}");
    Console.WriteLine($"settings:    {paths.SettingsFile}");
    return 0;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"未知命令: {command}");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("TinyBattery.Cli");
    Console.WriteLine("  validate <file>                校验设备定义 JSON");
    Console.WriteLine("  validate-theme <file|folder>   校验图标主题");
    Console.WriteLine("  validate-package <file>        校验扩展包 manifest");
    Console.WriteLine("  definitions                    列出内置 + 用户设备定义");
    Console.WriteLine("  themes                         列出图标主题");
    Console.WriteLine("  plugins                        列出已加载插件");
    Console.WriteLine("  hid                            列出 HID 接口和匹配结果");
    Console.WriteLine("  probe                          只读探测一次设备电量");
    Console.WriteLine("  select-theme <id>              选择图标主题");
    Console.WriteLine("  import-device <path>           导入设备定义");
    Console.WriteLine("  import-theme <path>            导入图标主题");
    Console.WriteLine("  import-package <path>          导入扩展包");
    Console.WriteLine("  paths                          显示数据目录");
}

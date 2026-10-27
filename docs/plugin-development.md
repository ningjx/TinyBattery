# 插件开发规范

TinyBattery 的设备和主题都通过插件提供。插件分为两类：

| 类型 | 内容 | 是否需要 DLL |
|---|---|---|
| 数据插件 | 设备定义 JSON、SVG 主题 | 否 |
| 代码插件 | 设备定义 + 自定义电池协议 | 是 |

用户主题统一使用 SVG；图片主题和旧版 `classic` 主题仅作为历史兼容读取，不再允许导入。

## 目录结构

一个插件就是一个包含 `plugin.json` 的文件夹。

数据插件：

```text
my-plugin/
  plugin.json
  devices/
    my-device.json
  themes/
    my-theme/
      theme.json
      icon.svg
```

代码插件：

```text
my-plugin/
  plugin.json
  MyPlugin.dll
  MyPlugin.deps.json
  devices/
  themes/
```

插件目录放在以下任一位置：

- `%LOCALAPPDATA%\TinyBattery\plugins\<插件名>\`
- exe 同级的 `plugins\<插件名>\`
- 单文件解压目录的 `plugins\<插件名>\`

内置插件 `logitech` 和 `classic-theme` 会优先使用内置资源；如果上述目录中存在同名 ID 的外部插件，则使用外部插件。不要复用这两个 ID。

## plugin.json

```json
{
  "$schema": "../../schemas/plugin.schema.json",
  "schemaVersion": 1,
  "id": "com.example.my-plugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "description": "Device definitions and themes for my hardware.",
  "apiVersion": "1.0",
  "assembly": "MyPlugin.dll",
  "entryType": "MyPlugin.MyPluginEntry",
  "devices": ["devices"],
  "themes": ["themes"]
}
```

字段：

| 字段 | 必填 | 说明 |
|---|---|---|
| `schemaVersion` | 是 | 固定为 `1` |
| `id` | 是 | 插件唯一 ID，小写字母/数字/`.`/`_`/`-` |
| `name` | 是 | 显示名称 |
| `version` | 是 | 插件版本，建议 SemVer |
| `description` | 否 | 描述 |
| `apiVersion` | 否 | 插件目标 API，当前主版本为 `1` |
| `assembly` | 代码插件必填 | 相对于插件目录的 DLL 路径 |
| `entryType` | 代码插件必填 | 实现 `ITinyBatteryPlugin` 的完整类型名 |
| `devices` | 否 | 设备定义文件或文件夹，可多个 |
| `themes` | 否 | 主题清单文件或文件夹，可多个 |

`assembly` 和 `entryType` 必须同时出现。

## 代码插件入口

```csharp
using TinyBattery.Abstractions.Plugins;

namespace MyPlugin;

public sealed class MyPluginEntry : ITinyBatteryPlugin
{
    public string Id => "com.example.my-plugin";

    public Version ApiVersion => new(1, 0, 0);

    public void Register(IPluginContext context, IPluginRegistrar registrar)
    {
        registrar.AddProtocol(new MyBatteryProtocol());
    }
}
```

要求：

- 类型必须是 `public`，有公共无参构造函数。
- 必须实现 `ITinyBatteryPlugin`。
- `ApiVersion.Major` 必须为 `1`。
- 插件会在独立的 `AssemblyLoadContext` 中加载。
- `TinyBattery.Abstractions` 等共享程序集由宿主提供，插件不要自带另一份。

`IPluginRegistrar` 提供：

```csharp
void AddProtocol(IBatteryProtocol protocol);
void AddIconTheme(IBatteryIconThemePlugin theme);
void AddDeviceDefinitionPath(string path);
void AddThemeManifestPath(string path);
```

当前可选择主题只支持 SVG 文件主题；`AddIconTheme` 注册的代码主题不会进入主题选择列表，建议不要依赖它。

`IPluginContext` 提供插件目录和 `ILoggerFactory`。

## 协议插件

协议实现 `IBatteryProtocol`：

```csharp
public interface IBatteryProtocol
{
    string Kind { get; }

    Task<IReadOnlyList<ProtocolReading>> ReadAsync(
        ProtocolContext context,
        CancellationToken cancellationToken);
}
```

设备定义里的 `protocol.kind` 必须与某个已注册协议的 `Kind` 一致。

只读原则：电量协议只应发送识别设备、发现特征和读取电量所需的请求，不应修改设备配置。

内置协议：

- `hid.request-response`：通过配置描述请求/响应报文，适合简单 HID 设备。
- `logitech.hidpp2`：Logitech HID++ 2.0 电量读取。

协议选项由协议实现自行解释；设备定义 schema 只对 `hid.request-response` 的选项做结构校验。

## 加载与校验

1. 读取 `plugin.json`，按 schema 校验。
2. 检查插件 ID 是否重复、`apiVersion` 是否兼容。
3. 展开 `devices` 和 `themes` 路径，加入资产目录。
4. 如果配置了 `assembly`/`entryType`，加载 DLL 并调用 `Register`。
5. 设备定义和主题在后续 Store 加载时再次做 schema、协议和资源校验。

插件失败不会阻止应用启动；错误会显示在设置页诊断区域。

## 版本与兼容

- 插件 `version` 用于展示、包管理和更新判断。
- `apiVersion` 当前只支持 `1.x`。
- 设备定义和主题各自有版本；包清单里的版本必须与文件版本一致。
- 新增字段优先保持向后兼容；破坏性变更需要提升 schema 版本或 API 主版本。

## 调试

```bash
# 查看插件、协议、主题清单
dotnet run --project src/TinyBattery.Cli -- plugins

# 查看 HID 接口和匹配结果
dotnet run --project src/TinyBattery.Cli -- hid

# 只读读取一次电量
dotnet run --project src/TinyBattery.Cli -- probe

# 校验单个设备定义
dotnet run --project src/TinyBattery.Cli -- validate path/to/device.json

# 校验主题
dotnet run --project src/TinyBattery.Cli -- validate-theme path/to/theme
```

应用日志输出到控制台；设置页诊断区域会显示插件和资产加载错误。单文件发布时如果从资源管理器启动，建议通过 CLI 或终端运行以查看日志。

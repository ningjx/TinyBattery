# 扩展包与更新源规范

TinyBattery 使用 `.tbpack` 包分发设备定义和 SVG 主题。一个包可以同时包含多个设备、多个主题和各自的版本信息。

## 包结构

```text
my-pack.tbpack        # 本质是 zip
  package.json
  devices/
    my-keyboard.json
  themes/
    neon/
      theme.json
      icon.svg
```

`package.json`：

```json
{
  "$schema": "../../schemas/package.schema.json",
  "schemaVersion": 1,
  "id": "com.example.my-pack",
  "name": "My Pack",
  "version": "1.2.0",
  "minAppVersion": "0.1.0",
  "author": "Ning",
  "description": "Devices and themes for my hardware.",
  "devices": [
    {
      "id": "com.example.my-keyboard",
      "version": "1.0.0",
      "path": "devices/my-keyboard.json"
    }
  ],
  "themes": [
    {
      "id": "com.example.neon",
      "version": "1.0.0",
      "path": "themes/neon"
    }
  ]
}
```

条目也可以简写为路径字符串：

```json
"devices": ["devices/my-keyboard.json"],
"themes": ["themes/neon"]
```

简写时版本从设备/主题文件自身读取。

## 字段

| 字段 | 必填 | 说明 |
|---|---|---|
| `schemaVersion` | 是 | 固定为 `1` |
| `id` | 是 | 包唯一 ID |
| `name` | 是 | 包名称 |
| `version` | 是 | 包版本，SemVer |
| `minAppVersion` | 否 | 最低应用版本；低于该版本拒绝导入 |
| `author` / `description` | 否 | 展示信息 |
| `devices` | 二选一 | 设备定义条目 |
| `themes` | 二选一 | 主题条目 |
| `metadata` | 否 | 自定义字符串键值 |

设备/主题条目对象：

| 字段 | 必填 | 说明 |
|---|---|---|
| `path` | 是 | 包内相对路径，不能逃出包目录 |
| `id` | 否 | 声明后必须与文件内 ID 一致 |
| `version` | 否 | 声明后必须与文件内版本一致 |

## 导入规则

应用只有一个导入入口：设置页“导入主题或设备…”，或 CLI：

```bash
dotnet run --project src/TinyBattery.Cli -- import-package path/to/my-pack.tbpack --overwrite
```

导入流程：

1. 解压到临时目录，检查 zip 路径穿越、文件数、大小和扩展名。
2. 读取 `package.json`；没有清单时扫描包内的 `theme.json` 和设备定义 JSON。
3. 校验所有设备定义、协议注册、主题 SVG、版本一致性。
4. 检查 ID 冲突和覆盖选项。
5. 通过后再写入 `devices/` 和 `themes/`。
6. 记录到 `installed-packages.json`。

约束：

- `.tbpack` 只允许数据内容：设备定义 JSON 和 SVG 主题。
- 代码插件 DLL 不通过包导入，需要作为插件文件夹放入 `plugins`。
- 包内重复的设备 ID 或主题 ID 会拒绝。
- 主题必须是标准 SVG 主题；旧版 `classic` / `imageSet` 主题会拒绝。

## 版本规则

- 包、设备、主题都使用 SemVer。
- 包版本用于判断整体更新。
- 设备/主题版本用于展示和单项差异判断。
- `package.json` 中声明的条目版本必须与文件内部 `version` 一致，否则导入失败。
- 单独导入设备/主题时不强制版本，但建议始终填写。

## 安装记录

`%LOCALAPPDATA%\TinyBattery\installed-packages.json` 记录来源和版本：

```json
{
  "packages": [
    {
      "id": "com.example.my-pack",
      "version": "1.2.0",
      "source": "https://example.com/packages/my-pack-1.2.0.tbpack",
      "installedAt": "2026-10-27T12:00:00Z",
      "items": [
        {
          "kind": "theme",
          "id": "com.example.neon",
          "version": "1.0.0",
          "path": "C:\\Users\\...\\themes\\neon"
        }
      ]
    }
  ]
}
```

手动导入的包 `source` 是本地路径；自动识别、没有 `package.json` 的本地内容会生成 `local.<随机值>` 记录，不参与远程更新匹配。

## 更新源

更新源是一个静态 HTTPS JSON 文件。示例：

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-10-27T12:00:00Z",
  "packages": [
    {
      "id": "com.example.my-pack",
      "name": "My Pack",
      "version": "1.3.0",
      "minAppVersion": "0.1.0",
      "url": "https://example.com/packages/my-pack-1.3.0.tbpack",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "size": 123456,
      "notes": "Adds a new theme.",
      "items": {
        "devices": [
          {
            "id": "com.example.my-keyboard",
            "version": "1.1.0"
          }
        ],
        "themes": [
          {
            "id": "com.example.neon",
            "version": "1.1.0"
          }
        ]
      }
    }
  ]
}
```

字段：

| 字段 | 说明 |
|---|---|
| `id` | 与已安装包的 `id` 对应 |
| `version` | 新包版本 |
| `minAppVersion` | 不兼容的应用版本会被标记为不可安装 |
| `url` | HTTPS 包地址 |
| `sha256` | 包的 SHA-256；下载后必须匹配 |
| `size` | 可选，字节数 |
| `notes` | 更新说明 |
| `items.devices` / `items.themes` | 条目版本，用于显示单项差异 |

## 检查与安装

在“关于与更新”页面填写更新源并保存，或修改 `settings.json`：

```json
{
  "updateFeedUrl": "https://example.com/tinybattery/feed.json",
  "checkForUpdatesOnStartup": true,
  "autoUpdate": false
}
```

行为：

- 启动时可选后台检查；只检查并提示，不自动安装。
- 手动点击“检查更新”立即检查。
- 有更新时，托盘菜单的“关于与更新”会显示数量。
- 安装包前校验 HTTPS、大小和 SHA-256。
- 安装复用统一导入器，保留当前主题选择和设备槽位分配。
- 当前更新单位是整个包；条目版本用于展示差异。

## 当前限制

- 没有包签名校验；更新源自身被篡改不在当前防护范围内。
- 没有自动回滚到上一版本；失败时不会保留上一套安装内容。
- `autoUpdate` 保留为配置项，当前版本不会自动安装。
- 代码插件不参与远程更新。

## 校验

```bash
# 包清单 schema
dotnet run --project src/TinyBattery.Cli -- validate-package path/to/package.json

# 设备定义
dotnet run --project src/TinyBattery.Cli -- validate path/to/device.json

# 主题
dotnet run --project src/TinyBattery.Cli -- validate-theme path/to/theme
```

导入前建议先跑一次 `validate-package`，再使用 `import-package` 做完整校验和安装。

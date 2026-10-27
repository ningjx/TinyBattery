# TinyBattery

Windows 托盘电量监视器。应用本体只提供框架，设备支持和图标主题都以插件形式加载。

## 特性

- USB HID / HID++ 设备电量读取，支持多接口、多连接方式匹配。
- SVG 图标主题，单主题可同时显示多个设备槽位。
- 托盘右键菜单直接切换主题、分配设备。
- 统一 `.tbpack` 包导入，包内可混合多个设备定义和主题。
- 设备、主题、包都有版本号；支持 HTTPS 更新源检查与安装。
- 键盘/鼠标活动、系统恢复、用户解锁后自动刷新休眠设备。
- 可选的 Windows 开机自启。
- 支持 self-contained 单文件发布。

## 文档

- [插件开发规范](docs/plugin-development.md)
- [设备定义规范](docs/device-definitions.md)
- [SVG 图标主题规范](docs/icon-themes.md)
- [扩展包与更新源规范](docs/packages-and-updates.md)

## 构建与测试

```bash
dotnet build TinyBattery.slnx
dotnet test TinyBattery.slnx
```

运行应用：

```bash
dotnet run --project src/TinyBattery.App
```

只打开设置窗口：

```bash
dotnet run --project src/TinyBattery.App -- --settings
```

## 单文件发布

发布配置位于 `src/TinyBattery.App/Properties/PublishProfiles/win-x64.pubxml`：

```bash
dotnet publish src/TinyBattery.App/TinyBattery.App.csproj -p:PublishProfile=win-x64
```

输出：

```text
artifacts/publish/win-x64/TinyBattery.exe
```

该发布为 win-x64、self-contained、单文件。内置 Logitech 设备定义和 Classic SVG 主题嵌入 exe，首次运行释放到 `%LOCALAPPDATA%\TinyBattery\bundled\<版本>`，不需要外部 `plugins` 文件夹。

## 数据目录

默认根目录：`%LOCALAPPDATA%\TinyBattery`

```text
TinyBattery/
  settings.json
  installed-packages.json
  devices/           # 用户导入的设备定义
  themes/            # 用户导入的 SVG 主题
  plugins/           # 用户插件
  bundled/           # 内置插件释放目录
  updates/           # 更新包临时缓存
  backups/
  logs/              # 保留目录
```

插件扫描目录：

1. `%LOCALAPPDATA%\TinyBattery\plugins`
2. exe 同级的 `plugins` 目录
3. 进程基础目录下的 `plugins`（单文件解压目录）

## CLI

```bash
dotnet run --project src/TinyBattery.Cli -- <命令>
```

常用命令：

```text
validate <device.json>                校验设备定义
validate-theme <theme.json|文件夹>    校验 SVG 主题
validate-package <package.json>       校验包清单
definitions                           列出已加载设备定义
themes                                列出已加载主题
plugins                               列出已加载插件与协议
hid                                   列出 HID 接口及匹配结果
probe                                 只读探测已匹配设备的电量
import-device <路径> [--overwrite]
import-theme <路径> [--overwrite]
import-package <路径> [--overwrite]
select-theme <theme-id>
paths                                 输出数据目录
```

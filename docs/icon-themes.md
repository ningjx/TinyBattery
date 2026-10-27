# SVG 图标主题规范

当前可选择、可导入的主题只有一种标准格式：**SVG 主题**。

- 一个主题是一个文件夹，包含 `theme.json` 和至少一个 SVG。
- 主题声明固定槽位数量；一个图标可以显示 1–8 个设备的电量。
- 不需要为不同电量准备多张图片；填充、颜色和状态由渲染器完成。
- `classic` 和 `imageSet` 是历史格式，不再允许导入，也不会出现在主题列表中。内置的 Classic 主题本身也是 SVG。

## 目录结构

```text
my-theme/
  theme.json
  icon.svg
  states/            # 可选
    unknown.svg
    charging.svg
```

## 最小示例

```json
{
  "$schema": "../../../schemas/icon-theme.schema.json",
  "schemaVersion": 1,
  "id": "com.example.neon",
  "name": "Neon",
  "version": "1.0.0",
  "author": "Ning",
  "description": "Single-slot SVG battery.",
  "kind": "svg",
  "slotCount": 1,
  "render": {
    "svg": "icon.svg",
    "fillDirection": "vertical",
    "colors": {
      "healthy": "#3DDC84",
      "warning": "#FFB020",
      "critical": "#FF5A5F",
      "unknown": "#8C8C8C"
    },
    "slots": [
      {
        "id": "main",
        "label": "设备",
        "mode": "rect",
        "target": "tb-fill"
      }
    ]
  }
}
```

`icon.svg`：

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
  <defs>
    <rect id="tb-fill" x="4" y="8" width="24" height="16" rx="3" />
  </defs>

  <rect x="1.5" y="7.5" width="29" height="17" rx="4.5"
        fill="#2A2A2A" fill-opacity="0.18" />
  <rect x="4" y="8" width="24" height="16" rx="3"
        fill="none" stroke="#FFFFFF" stroke-opacity="0.2" />
</svg>
```

`id="tb-fill"` 的几何只用于渲染器计算填充，不要求它显示出来，推荐放在 `<defs>` 中。

## 顶层字段

| 字段 | 必填 | 说明 |
|---|---|---|
| `schemaVersion` | 是 | 固定为 `1` |
| `id` | 是 | 主题唯一 ID；`classic`、`fallback` 保留 |
| `name` | 是 | 显示名称 |
| `version` | 建议 | 主题版本；包内声明版本时必须一致 |
| `author` | 否 | 作者 |
| `description` | 否 | 描述 |
| `kind` | 是 | 固定为 `svg` |
| `slotCount` | 是 | 固定槽位数量 1–8，必须等于 `render.slots` 数量 |
| `render` | 是 | 渲染配置 |
| `appIcon` | 否 | 可选 PNG/ICO，用作窗口图标 |
| `metadata` | 否 | 自定义字符串键值 |

主题没有“设备数量范围”。如果同一套图要支持 1 个和 2 个设备，应发布为两个主题 ID，或使用一个固定的 2 槽主题，未分配槽位显示为未知。

## render 配置

```json
"render": {
  "svg": "icon.svg",
  "fillDirection": "vertical",
  "startAngle": -90,
  "clockwise": true,
  "innerRadius": 0,
  "colors": {
    "healthy": "#3DDC84",
    "warning": "#FFB020",
    "critical": "#FF5A5F",
    "unknown": "#8C8C8C",
    "track": "#33000000"
  },
  "slots": [ ],
  "states": {
    "unknown": "states/unknown.svg",
    "charging": "states/charging.svg"
  }
}
```

| 字段 | 默认 | 说明 |
|---|---|---|
| `svg` | 必填 | 基础 SVG 路径 |
| `fillDirection` | `vertical` | `rect` 填充方向：`vertical` 从下往上，`horizontal` 从左往右 |
| `startAngle` | -90 | 圆环/扇形起始角度，-90 为顶部 |
| `clockwise` | true | 是否顺时针 |
| `innerRadius` | 0 | 扇形内半径；大于 0 时绘制环形扇区 |
| `colors.*` | 见下 | 状态颜色，支持 `#RRGGBB` 或 `#AARRGGBB` |
| `slots` | 必填 | 槽位数组，数量等于 `slotCount` |
| `states` | 否 | 可选状态覆盖 SVG |

颜色阈值：

- `percent >= 60`：`healthy`
- `20 <= percent < 60`：`warning`
- `percent < 20`：`critical`
- 没有数据：`unknown`

`colors.track` 只用于圆环背景；如果 SVG 本身已经画了轨道，可以不填。

## 槽位与填充

```json
"slots": [
  {
    "id": "left",
    "label": "左耳",
    "mode": "ring",
    "target": "tb-ring-left"
  },
  {
    "id": "right",
    "label": "右耳",
    "mode": "ring",
    "target": "tb-ring-right"
  }
]
```

| 字段 | 说明 |
|---|---|
| `id` | 槽位唯一 ID，小写字母/数字/`.`/`_`/`-` |
| `label` | 设置页和托盘菜单中显示的名称 |
| `mode` | `rect`、`ring`、`sector`、`tint` |
| `target` | SVG 中 `id` 对应的目标元素 |

### `rect`

`target` 必须是 `<rect>`，渲染器读取 `x/y/width/height`，按百分比填充。

- `fillDirection: vertical`：从底部向上。
- `fillDirection: horizontal`：从左侧向右。
- 目标区域不需要透明；填充会画在基础 SVG 之上。
- 需要圆角时，由基础外壳遮挡，或让作者自行在基础图上处理。

### `ring`

`target` 必须是 `<circle>`，渲染器读取 `cx/cy/r` 和 `stroke-width`，绘制进度弧。

- 起点由 `startAngle` 控制，默认顶部。
- 方向由 `clockwise` 控制。
- 如果设置 `colors.track`，渲染器会先画一圈轨道。
- 进度弧使用圆头端点；满分时绘制整圆。

### `sector`

`target` 必须是 `<circle>`，渲染器以圆心和半径绘制扇形。

- `innerRadius: 0`：实心饼图。
- `innerRadius > 0`：环形扇区。
- 起始角度和方向同样使用 `startAngle` / `clockwise`。

### `tint`

只允许单槽主题。渲染器会把整个基础 SVG 按照当前状态颜色着色，不做局部填充。适合单色图标或表情类图标。

## SVG 约束

- 必须有 `viewBox`，或同时有 `width` / `height`。
- `target` 元素必须在 SVG 中存在，且模式与元素类型匹配。
- `target` 元素不能带 `transform`。
- 禁止 `<script>`、`<foreignObject>`、`<image>`。
- 禁止外部 `href`；只允许 `#local-id` 引用。
- 单文件不超过 256 KB。
- 文本请转成路径，避免依赖系统字体。
- 支持 `mask`、`clipPath`、渐变等常规内联图形；复杂滤镜/混合模式不保证与设计工具一致。

渲染顺序：

1. 基础 SVG；
2. 各槽位填充；
3. 充电覆盖层。

如果使用了状态覆盖 SVG，则用覆盖图替代“基础 SVG + 填充”，然后叠加充电层。

## 状态覆盖

`states` 可选，键为：

- `full`：>= 90%
- `high`：>= 60%
- `mid`：>= 20%
- `low`：< 20%
- `unknown`：无数据
- `charging`：充电覆盖层，叠加在最终图标上

示例：

```json
"states": {
  "unknown": "states/unknown.svg",
  "charging": "states/charging.svg"
}
```

状态覆盖适合“某个电量档位必须完全不同构图”的情况。普通主题不需要提供这些文件。

## 多设备主题

一个主题固定显示 N 个设备，就声明 `slotCount: N` 和 N 个槽位。设置页和托盘右键菜单会为每个槽位提供设备选择。

- 未分配的槽位显示为未知/空。
- 设备未连接时，槽位保留，不会自动换设备。
- 超过槽位数量的设备不会显示。
- 同一个设备可以重复分配到多个槽位，但通常没有意义。

示例可参考 `samples/themes/dual-ring`。

## 校验与调试

```bash
dotnet run --project src/TinyBattery.Cli -- validate-theme samples/themes/neon-svg
```

校验内容：

- `theme.json` schema；
- `slotCount` 与槽位数量；
- SVG 文件存在、大小、XML 安全约束；
- `target` 元素存在且类型正确；
- 槽位 ID 唯一；
- 状态文件可解析。

导入时还会做一次完整校验，失败不会写入主题目录。

## 版本与命名

- 新主题请使用 `kind: "svg"`。
- 不要使用 `classic` 或 `fallback` 作为主题 ID。
- 包内声明主题版本时，`package.json` 的版本必须与 `theme.json` 一致。
- 主题更新可以改变 `slotCount`；已有设备分配会按顺序保留，超出新槽位数量的部分仍保存在设置里，但当前主题不会使用。

# 设备定义规范

设备定义是 JSON 文件，描述“如何找到设备”和“如何读取电量”。设备定义可以单独导入，也可以放进插件或 `.tbpack` 包。

## 最小示例

```json
{
  "$schema": "../../schemas/device-definition.schema.json",
  "schemaVersion": 2,
  "version": "1.0.0",
  "id": "com.example.my-keyboard",
  "displayName": "Example Keyboard",
  "vendor": "Example",
  "match": {
    "transport": "hid",
    "vendorId": 1133,
    "productId": 50501,
    "usagePage": 65280,
    "usage": 2,
    "interface": "MI_02"
  },
  "protocol": {
    "kind": "hid.request-response",
    "options": { }
  },
  "polling": {
    "intervalSeconds": 60,
    "timeoutSeconds": 2,
    "jitterSeconds": 5
  }
}
```

## 顶层字段

| 字段 | 必填 | 说明 |
|---|---|---|
| `schemaVersion` | 是 | `1` 或 `2` |
| `version` | 建议 | 设备定义版本；包内声明版本时必须提供且一致 |
| `id` | 是 | 唯一 ID，小写字母/数字/`.`/`_`/`-` |
| `displayName` | 是 | 显示名称 |
| `vendor` | 否 | 厂商 |
| `enabled` | 否 | 默认 `true`；`false` 不会被监控 |
| `priority` | 否 | 匹配同分时的优先级，默认 `0` |
| `match` | 二选一 | 单条匹配规则 |
| `matches` | 二选一 | 多条匹配规则，最多 8 条；适合同一设备的多种连接方式 |
| `protocol` | 是 | 协议类型和选项 |
| `polling` | 否 | 轮询参数 |
| `metadata` | 否 | 自定义字符串键值 |
| `$schema` | 否 | JSON Schema 路径 |

`match` 和 `matches` 至少提供一个。推荐新设备使用 `schemaVersion: 2` 和 `matches`。

## 多连接方式：一个设备一个定义

同一把键盘可能通过接收器、有线 USB、共享接收器暴露成不同 PID。不要写三个定义，写一个定义：

```json
{
  "schemaVersion": 2,
  "version": "1.1.0",
  "id": "logitech.g913-tkl",
  "displayName": "Logitech G913 TKL",
  "matches": [
    {
      "label": "Receiver",
      "transport": "hid",
      "vendorId": 1133,
      "productId": 50501,
      "usagePage": 65280,
      "usage": 2,
      "interface": "MI_02"
    },
    {
      "label": "USB",
      "transport": "hid",
      "vendorId": 1133,
      "productId": 49987,
      "usagePage": 65280,
      "usage": 2,
      "interface": "MI_02"
    }
  ],
  "protocol": {
    "kind": "logitech.hidpp2",
    "options": {
      "softwareId": 10,
      "deviceIndexes": [1, 255, 2, 3, 4, 5, 6],
      "batteryFeatures": ["0x1001", "0x1000", "0x1004"]
    }
  }
}
```

设置页只显示实际连接的设备；定义本身只在后台参与匹配。

## 匹配规则

每条规则支持：

| 字段 | 说明 |
|---|---|
| `transport` | `hid`、`bleGatt`、`pnpProperty` |
| `label` | 可选，用于日志标识连接方式 |
| `vendorId` / `productId` | USB VID/PID |
| `usagePage` / `usage` | HID Usage Page / Usage |
| `interface` | 如 `MI_02` |
| `inputReportLength` / `outputReportLength` | HID 报告长度 |
| `productRegex` | 产品名正则 |
| `manufacturerRegex` | 厂商名正则 |
| `serialRegex` | 序列号正则 |
| `serviceUuid` | BLE GATT 服务 UUID |
| `deviceClass` | 设备类别 |

规则中写出的字段必须全部匹配；未写出的字段不参与判断。至少需要提供 `vendorId`、`usagePage`、`productRegex`、`manufacturerRegex`、`serialRegex`、`serviceUuid`、`deviceClass` 之一。

匹配评分（只用于同一接口命中多条定义时）：

- VID/PID：20 分
- Usage Page / Usage：15 分
- interface：10 分
- service UUID：30 分
- device class / product regex / serial regex：10 分
- 报告长度：5 分
- manufacturer regex：5 分

取最高分；同分时取 `priority` 更高的定义。

## 协议

```json
"protocol": {
  "kind": "hid.request-response",
  "options": { }
}
```

`kind` 必须对应已加载插件注册的 `IBatteryProtocol.Kind`。

### `hid.request-response`

通过 JSON 描述一次固定请求和响应解析：

```json
{
  "kind": "hid.request-response",
  "options": {
    "reportId": 1,
    "includeReportId": true,
    "timeoutMs": 2000,
    "request": {
      "payload": [1, 0, 0, 0, 0, 0, 0, 0],
      "checksum": {
        "kind": "sum8",
        "range": [1, 7],
        "at": 7
      }
    },
    "response": {
      "match": [
        { "offset": 0, "equals": 1 }
      ],
      "minLength": 2
    },
    "extract": {
      "percent": {
        "offset": 1,
        "type": "u8",
        "min": 0,
        "max": 100
      },
      "charging": {
        "offset": 2,
        "type": "u8",
        "map": {
          "0": "Discharging",
          "1": "Charging",
          "2": "Full",
          "3": "Error"
        }
      }
    }
  }
}
```

- `request.payload` 不含 report ID；`includeReportId: true` 时自动在前面加 `reportId`。
- `request.checksum` 支持 `none`、`sum8`、`xor8`、`crc8`、`crc16`、`crc32`。
- `response.match` 每项用 `equals`，或 `mask` + `value` 匹配字节。
- `response.minLength` 防止过短响应被解析。
- `extract` 支持字段名：
  - `percent`：0–100，超出范围会报错。
  - `voltageMv` 或 `voltage`：电压。
  - `charging`：值是数字，配合 `map` 转成 `ChargingState` 字符串。
- 其他提取字段会计算但不会进入最终 `BatteryReading`。

字段类型：

```text
u8 i8 u16le u16be i16le i16be u24le u24be
u32le u32be i32le i32be bits
```

`bits` 需要 `bitOffset`、`bitCount`；还可以使用 `mask`、`shift`、`min`、`max`、`map`。

### `logitech.hidpp2`

只发送 HID++ 特征发现和电量查询，只读，不修改键盘配置。

```json
"options": {
  "softwareId": 10,
  "deviceIndexes": [1, 255, 2, 3, 4, 5, 6],
  "scanAllDeviceIndexes": false,
  "batteryFeatures": ["0x1001", "0x1000", "0x1004"]
}
```

- `deviceIndexes`：接收器下的设备索引，按顺序尝试。
- `scanAllDeviceIndexes: true` 时会继续读取其他索引，适合一个接收器带多个设备。
- `batteryFeatures`：按顺序尝试的 HID++ 电量特征。
- 读取结果可能是一个或多个 `ProtocolReading`；多个设备时逻辑 ID 会带 `/索引` 后缀。

## 轮询

```json
"polling": {
  "intervalSeconds": 60,
  "timeoutSeconds": 2,
  "jitterSeconds": 5,
  "retry": {
    "maxAttempts": 2,
    "backoffMs": 300
  }
}
```

| 字段 | 默认 | 说明 |
|---|---|---|
| `intervalSeconds` | 60 | 正常轮询间隔，最小 5 秒 |
| `timeoutSeconds` | 2 | 单次协议读取超时 |
| `jitterSeconds` | 5 | 轮询随机抖动，避免多设备同时请求 |
| `retry.maxAttempts` | 2 | 协议内部尝试次数 |
| `retry.backoffMs` | 300 | 协议内部重试间隔 |

运行时的失败退避为 5、10、20、30 秒（上限 30 秒）。系统恢复、用户解锁或键盘/鼠标活动会立即请求刷新，不必等待下一次轮询。

## 校验与调试

```bash
dotnet run --project src/TinyBattery.Cli -- validate path/to/device.json
dotnet run --project src/TinyBattery.Cli -- hid
dotnet run --project src/TinyBattery.Cli -- probe
```

- `validate` 只做 schema 校验；协议是否注册、设备是否能匹配需要运行 `hid` 或 `probe`。
- `hid` 列出所有 HID 接口及其匹配到的定义。
- `probe` 只发只读请求，适合确认电量协议。

## 版本规则

- 单独导入不强制 `version`。
- 放进带版本的 `.tbpack` 时，`package.json` 中声明的设备版本必须与设备文件一致。
- id 相同的新定义会覆盖旧定义；插件中的定义先加载，用户目录中的定义后加载。

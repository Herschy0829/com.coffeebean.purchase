# com.coffeebean.purchase

CoffeeBean 支付模块：基于 Unity IAP 5.4 的内购统一封装。

- **独立模块**：不依赖 CoffeeBean 任何其他模块，单独安装即可独立工作；工程装有 Core 时自动集成（注册进服务注册表，可由 Module Manager 管理装卸）
- **Excel 驱动配置**：编辑器内选择一张 Excel 表配置所有商品，校验后生成 `IapConfig.asset + .json`；打包前自动重新解析，保证配置最新
- **可选服务器二次确认**：无服务器时可正常购买；有服务器时通过 `IPurchaseVerifier` 做收据核销，未确认的购买保持 Pending，崩溃/断网后自动补发不丢单
- **恢复购买**：统一恢复接口，按平台处理

## 安装

```json
{
  "dependencies": {
    "com.coffeebean.purchase": "https://github.com/Herschy0829/com.coffeebean.purchase.git#v0.1.0"
  }
}
```

## 快速开始（配置管线，v0.1）

1. 打开 `Window > CoffeeBean > Purchase Config`
2. 点击「选择...」选取商品 Excel 表（列名见下方规范）→ 校验通过自动弹窗
3. 点击「重新生成配置」→ 生成 `Assets/Resources/CoffeeBean/IapConfig.asset`（+ .json 旁证）
4. 打包时自动重新解析；解析失败会中止打包并弹窗

## Excel 列规范

| 列名 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Id_s` | string | ✅ | 内部商品 ID（服务端对账/补发用），唯一 |
| `GoogleProductId_s` | string | ✅ | Google Play 商品 ID，唯一 |
| `AppleProductId_s` | string | ✅ | App Store 商品 ID，唯一 |
| `ConsumeType_i` | int | ✅ | 0=消耗型 1=非消耗型（2=订阅暂不支持） |
| `Title_s` | string | | 兜底显示名 |
| `Description_s` | string | | 兜底描述 |
| `Price_f` | float | | 价格锚点（实际价格以商店下发为准） |
| `Currency_s` | string | | 货币代码覆盖（3 位大写） |
| `Enabled_i` | int | | 0/1 上架开关，默认 1 |
| `Group_s` | string | | 分组/礼包标识 |
| `SortOrder_i` | int | | 排序 |
| `Verify_i` | int | | -1 跟随全局 / 0 否 / 1 是 |
| `Extra_s` | string | | 扩展透传（JSON） |

## 依赖

- `com.unity.purchasing` 5.4.x（Unity IAP）
- NPOI 2.6.2（Editor-only，Excel 解析，见 `Editor/NPOI/`）

## License

[MIT](LICENSE.md)

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

## 快速开始（运行时）

```csharp
using CoffeeBean.Purchase;

// 1. 初始化（config 由 Excel 生成；verifier 可选）
var service = IapService.Instance;
service.Initialize(config, serverVerifier /* IPurchaseVerifier 可选 */);
service.OnInitialized += () => Debug.Log("商品已下发");
service.OnPurchaseSucceeded += order => GrantItem(order.InternalId); // 发货

// 2. 查询商品（通过内部 ID 或平台 ID）
IapProduct p = service.GetProduct("gem_100");              // 内部 ID
IapProduct g = service.GetProductByGoogleId("com.xxx.gem"); // Google ID
IapProduct a = service.GetProductByAppleId("com.xxx.ios");  // Apple ID
Debug.Log($"价格: {p.LocalizedPriceString} 货币: {p.CurrencyCode}");

// 3. 购买 / 恢复购买
service.Purchase("gem_100");   // 服务器核销开启时自动走 IPurchaseVerifier，Pending 未确认自动补发
service.RestorePurchases();
```

> 无服务器时 `serverVerifier` 传 null 且关闭 `IapConfig.serverVerifyEnabled`，购买直接完成。
> 服务器核销开启时，未确认的购买保持 Pending，崩溃/断网后下次启动 `FetchPurchases` 自动补发。

## Excel 列规范

| 列名 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `Id_s` | string | ✅ | 内部商品 ID（服务端对账/补发用），唯一；为空视为注释行，跳过 |
| `GoogleProductId_s` | string | ✅ | Google Play 商品 ID，唯一；无效（空/占位符）则商品不参与初始化 |
| `AppleProductId_s` | string | ✅ | App Store 商品 ID，唯一；无效（空/占位符）则商品不参与初始化 |
| `ConsumeType_i` | int | ✅ | 0=消耗型 1=非消耗型 2=映射为非消耗型（礼包/特权类） |
| `IapType_i` | int | | 显式商店类型（可选，**优先于 ConsumeType_i**）：0=消耗 1=非消耗 2=订阅 |
| `Title_s` | string | | 兜底显示名 |
| `Description_s` | string | | 兜底描述 |
| `Price_f` | float | | 价格锚点（实际价格以商店下发为准；支持 `¥68` 这类字符串取数字部分） |
| `Currency_s` | string | | 货币代码覆盖（3 位大写） |
| `Enabled_i` | int | | 0/1 上架开关，默认 1 |
| `Group_s` | string | | 分组/礼包标识 |
| `SortOrder_i` | int | | 排序 |
| `Verify_i` | int | | -1 跟随全局 / 0 否 / 1 是 |
| `Extra_s` | string | | 扩展透传（JSON） |

> 支持别名与双行表头：字段名行可在前 3 行任意位置（如"中文说明行 + 字段名行"）；列名兼容 `ID_i`/`DefaultPrice_s`/中文表头等。

## 依赖

- `com.unity.purchasing` 5.4.x（Unity IAP）
- MiniExcel 1.46（Editor-only，Excel 解析，`Editor/MiniExcel/`，约 490KB / 4 个 DLL；仅支持 `.xlsx`）

## License

[MIT](LICENSE.md)

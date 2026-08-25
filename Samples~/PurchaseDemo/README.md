# Purchase Demo（完整购买流程示例）

覆盖支付模块完整流程：**初始化 → 商品下发缓存 → 查询 → 购买（可选服务器核销）→ 发货 → 失败处理 → 恢复购买**。

## 导入

Package Manager → `com.coffeebean.purchase` → **Samples → Purchase Demo → Import**。

## 使用（1 分钟跑通）

1. **生成配置**：`Window > CoffeeBean > Purchase Config` → 选你的商品 Excel → 重新生成配置
2. **建场景对象**：场景中新建空物体，挂上 **`PurchaseDemo`** 组件
3. 运行（Play）：
   - 默认 **useFakeStore = true**：编辑器里就能跑完整流程（假商店模拟下发/购买/恢复）
   - **demoServerVerify**：开启后演示"服务器二次确认"——购买后先等待核销（`DemoPurchaseVerifier` 模拟延迟），通过后才发货
4. 操作：点界面上的「购买」按钮 → 看 Console 日志（初始化 → 商品下发 → 购买 → 发货/失败）→ 点「恢复购买」

## 真机使用

- 关闭 **useFakeStore** → 自动走真实商店（`IapService.Instance`）
- 把 **DemoPurchaseVerifier** 换成你们自己的 `IPurchaseVerifier`（发收据给服务器，返回 Verified/Rejected/Error）

## 文件说明

| 文件 | 作用 |
|------|------|
| `PurchaseDemo.cs` | 主演示组件：初始化 / 事件订阅 / 购买 / 恢复 / IMGUI 面板 |
| `DemoStoreAdapter.cs` | 编辑器演示用假商店（模拟商店行为，真机不用） |
| `DemoPurchaseVerifier.cs` | 服务器核销示例实现（模拟延迟验证，真实项目替换为 HTTP 请求） |

## 事件全览（流程覆盖点）

| 事件 | 触发时机 |
|------|----------|
| `OnInitialized` | 初始化完成、商品已下发缓存 |
| `OnInitFailed` | 初始化失败（商店不可用/缺配置） |
| `OnProductUpdated` | 每个商品缓存更新（价格/货币/描述） |
| `OnPurchaseSucceeded` | 购买确认并已发货（在此发放权益） |
| `OnPurchaseFailed` | 购买失败（含原因：取消/网络/商店错误等） |
| `OnPurchasePending` | 购买待处理（等待商店/用户操作） |
| `OnPurchaseDeferred` | 购买延迟（如家长同意） |
| `OnRestoreFinished` | 恢复购买完成 |
| `OnRestoreFailed` | 恢复购买失败 |
| `OnPendingPurchaseReprocessed` | 历史购买补发（崩溃/断网恢复） |

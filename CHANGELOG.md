# Changelog

## [0.2.1] - 2026-08-28

### Changed
- **工具入口收敛到 CoffeeBean Hub**：内购配置窗口加 CoffeeBeanToolAttribute 标记（模块内复制同名定义，无需依赖 core），由 Window > CoffeeBean 统一发现打开；移除独立菜单项

# Changelog

## [0.2.0] - 2025-xx-xx

### Changed
- **统一命名空间**：全部类型迁移到 `CoffeeBean` 根命名空间（业务只需 `using CoffeeBean;` 即可使用所有模块主类型），模块内部辅助 / 测试 / 示例保留 `CoffeeBean.X` 子命名空间（父命名空间自动可见）
- **破坏性变更**：旧 `using CoffeeBean.X;` 需移除（类型已上移到根命名空间）

# Changelog

## [0.1.6] - 2025-xx-xx

### Changed
- **Excel 解析迁移到 excel 模块**：`ExcelImporter` 读取层（MiniExcel / 表头检测 / 列名别名归一 / 空行与 # 注释行跳过 / 问题分级）
  重构为基于 `com.coffeebean.excel` 的 `CExcelReader`；本模块只保留 IAP 特有校验
  （ConsumeType/IapType 映射、价格宽松解析、商店 ID 校验、货币/JSON 校验、重复检测）
- 新增依赖 `com.coffeebean.excel`（0.1.0）
- 行为不变：表头检测、列别名、警告分级、必填列校验与 v0.1.5 一致

## [0.1.5] - 2025-xx-xx

### Fixed
- **服务器拒绝核销不再静默**：`VerificationResult.Rejected` 时触发 `OnPurchaseFailed`
  （`FailureReason = "ServerRejected"`），业务方可提示玩家 / 走客服流程；
  订单保持未核销（交易未确认，人工判定有效后启动补发仍可恢复）
- 失败订单携带源订单的交易号 / 商店 / 收据（`FailOrder` 重载），便于定位与客服处理

### Changed
- 拒绝场景日志补充说明

## [0.1.4] - 2025-xx-xx

### Added
- **示例：Purchase Demo**（`Samples~/PurchaseDemo`，Package Manager 可一键导入）：
  完整购买流程演示——初始化、商品下发缓存、查询、购买（可选服务器核销）、发货、失败处理、恢复购买；
  内置假商店适配器（`DemoStoreAdapter`）让全流程在编辑器即可跑通，附 `DemoPurchaseVerifier` 服务器核销示例实现

## [0.1.3] - 2025-xx-xx

### Fixed
- **ConsumeType_i 映射修正**：按项目约定 **1 = 可消耗**（可重复购买）、**2 = 不可消耗**（礼包/永久增益）
  （v0.1.2 曾误映射 1 → 非消耗型；`IapType_i` 显式覆盖不受影响）

## [0.1.2] - 2025-xx-xx

### Changed
- Excel 解析大幅增强（适配真实项目表）：
  - **表头自动检测**：前 3 行中匹配规范列名最多的行作为表头（支持"中文说明行 + 字段名行"双行表头）
  - **列名别名**：兼容 `ID_i` / `DefaultPrice_s` / 中文表头（商品ID、Google商品ID 等）
  - **商店 ID 无效即跳过**：Google/Apple ID 为空或占位符（`-`/`0`/`待定` 等）时，该商品不参与初始化，警告级跳过
  - **类型映射**：`ConsumeType_i` 0=消耗 / 1=非消耗 / 2=映射非消耗（礼包特权类）；新增可选列 `IapType_i` 显式指定商店类型（0/1/2，优先）
  - **警告不阻塞**：注释行、空 ID 行、无效商店 ID 等为警告级，不中断生成；仅真正错误（非法类型、重复 ID、缺必填列）阻塞
  - 价格支持 `¥68` 这类字符串取数字部分

## [0.1.1] - 2025-xx-xx

### Changed
- Excel 解析从 NPOI（13 个 DLL / 17MB）换成 **MiniExcel 1.46**（4 个 DLL / ~490KB，Editor-only）
- 注意：MiniExcel 仅支持 `.xlsx`，不支持旧版 `.xls`（如需要可切 ExcelDataReader）

## [0.1.0] - 2025-xx-xx

### Added
- 配置管线：Excel 解析（NPOI）+ 校验弹窗 + 生成 `IapConfig.asset/.json`
- 编辑器窗口 `Window > CoffeeBean > Purchase Config`
- 打包监听 `IPreprocessBuildWithReport`（打包前强制重解析，失败中止）
- 运行时核心（Unity IAP 5.4）：
  - `IapService` 门面：初始化 / 商品缓存查询（内部 ID / Google ID / Apple ID / 平台 ID）
  - `UnityIapStoreAdapter`：Unity IAP v5 隔离层（唯一接触 UnityEngine.Purchasing 的文件）
  - 购买流程：防重入状态机、可选服务器二次确认（`IPurchaseVerifier`，超时+重试）、Pending 补发、交易去重日志
  - 恢复购买 `RestorePurchases`；历史购买拉取补发（崩溃/断网恢复）
  - `DisabledStoreAdapter` 兜底（编辑器/测试环境不炸）
- Core 可选集成：`CoffeeBean.Purchase.Bridge` 程序集（defineConstraints `COFFEEBEAN_CORE`），
  安装 Core 时自动注册 `IapService` 进服务注册表；无 Core 时完全独立运行
- EditMode 测试：Excel 管线 5 个 + 运行时全流程 11 个

### Notes
- v1 暂不支持订阅（ConsumeType=2 校验拦截）
- v5 中收据/交易 ID 以 Order（IOrderInfo）为准，不从 Product 缓存取

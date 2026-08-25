# Changelog

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

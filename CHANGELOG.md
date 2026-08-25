# Changelog

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

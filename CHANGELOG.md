# Changelog

## [0.1.0] - 2025-xx-xx

### Added
- 配置管线：Excel 解析（NPOI）+ 校验弹窗 + 生成 `IapConfig.asset/.json`
- 编辑器窗口 `Window > CoffeeBean > Purchase Config`
- 打包监听 `IPreprocessBuildWithReport`（打包前强制重解析，失败中止）
- 运行时配置模型（`IapConfig` / `IapProductDefinition` / `IapConsumeType`）
- 与 Core 的可选集成桥（versionDefines：`COFFEEBEAN_CORE`）

### Notes
- v1 暂不支持订阅（ConsumeType=2 校验拦截）
- 运行时购买/核销/恢复购买（Phase 3~5）待实现

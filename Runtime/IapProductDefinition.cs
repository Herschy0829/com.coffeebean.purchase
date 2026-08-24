using System;

namespace CoffeeBean.Purchase
{
    /// <summary>
    /// 商品定义（由 Excel 配置生成，见 design-iap.md §3）。
    /// 字段名与 Excel 列一一对应（列名 = 字段名 + 类型后缀）。
    /// </summary>
    [Serializable]
    public sealed class IapProductDefinition
    {
        /// <summary>内部商品 ID（服务端对账 / 补发用），对应 Excel Id_s。必填、唯一。</summary>
        public string internalId;

        /// <summary>Google Play 商品 ID，对应 Excel GoogleProductId_s。必填、唯一。</summary>
        public string googleProductId;

        /// <summary>App Store 商品 ID，对应 Excel AppleProductId_s。必填、唯一。</summary>
        public string appleProductId;

        /// <summary>商品类型，对应 Excel ConsumeType_i（0/1，2 暂不支持）。必填。</summary>
        public IapConsumeType consumeType;

        /// <summary>展示名（商店未下发时的兜底），对应 Excel Title_s。</summary>
        public string title;

        /// <summary>描述（兜底），对应 Excel Description_s。</summary>
        public string description;

        /// <summary>价格锚点（仅展示/校验，实际价格以商店下发为准），对应 Excel Price_f。</summary>
        public float priceAnchor;

        /// <summary>货币代码覆盖（默认取商店下发），对应 Excel Currency_s，3 位大写字母。</summary>
        public string currency;

        /// <summary>上架开关，对应 Excel Enabled_i（0/1，默认 1）。</summary>
        public bool enabled = true;

        /// <summary>商品分组 / 礼包标识（透传），对应 Excel Group_s。</summary>
        public string group;

        /// <summary>排序，对应 Excel SortOrder_i。</summary>
        public int sortOrder;

        /// <summary>该商品是否服务器二次确认，对应 Excel Verify_i：-1=跟随全局，0=否，1=是。</summary>
        public int serverVerifyOverride = -1;

        /// <summary>扩展透传（JSON 字符串），对应 Excel Extra_s。</summary>
        public string extra;

        public override string ToString() => $"[{internalId}] {title} ({googleProductId} / {appleProductId}) {consumeType}";
    }
}

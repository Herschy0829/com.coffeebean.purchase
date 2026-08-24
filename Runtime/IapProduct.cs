namespace CoffeeBean.Purchase
{
    /// <summary>
    /// 运行时商品数据：Excel 定义 + 商店下发缓存（价格/描述/货币等）。
    /// 通过内部 ID 或平台 ID 查询（见 IapService）。
    /// </summary>
    public sealed class IapProduct
    {
        /// <summary>内部商品 ID（对应 Excel Id_s）。</summary>
        public string InternalId;

        /// <summary>Google Play 商品 ID。</summary>
        public string GoogleProductId;

        /// <summary>App Store 商品 ID。</summary>
        public string AppleProductId;

        /// <summary>商品类型。</summary>
        public IapConsumeType ConsumeType;

        /// <summary>商店是否可购买（商品下发成功且可用）。</summary>
        public bool Available;

        /// <summary>商店下发的本地化标题。</summary>
        public string Title;

        /// <summary>商店下发的本地化描述。</summary>
        public string Description;

        /// <summary>商店下发的货币代码（如 USD/CNY）。</summary>
        public string CurrencyCode;

        /// <summary>商店下发的本地化价格字符串（如 "$1.99"）。</summary>
        public string LocalizedPriceString;

        /// <summary>商店下发的价格数值。</summary>
        public decimal LocalizedPrice;

        /// <summary>最近一次交易的收据（JSON）。</summary>
        public string Receipt;

        /// <summary>最近一次交易的 ID。</summary>
        public string TransactionId;

        /// <summary>是否有收据（交易未完成/可核销）。</summary>
        public bool HasReceipt => !string.IsNullOrEmpty(TransactionId) && !string.IsNullOrEmpty(Receipt);

        /// <summary>当前平台对应的商店 ID。</summary>
        public string PlatformId => IapPlatform.GetPlatformId(this);

        /// <summary>价格展示字符串（商店未下发时用 Excel 锚点兜底）。</summary>
        public string GetDisplayPrice(float priceAnchor)
        {
            if (!string.IsNullOrEmpty(LocalizedPriceString)) return LocalizedPriceString;
            return priceAnchor > 0 ? priceAnchor.ToString("0.##") : string.Empty;
        }

        public override string ToString() => $"[{InternalId}] {PlatformId} price={LocalizedPriceString} available={Available}";
    }
}

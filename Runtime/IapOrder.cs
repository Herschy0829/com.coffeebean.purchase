namespace CoffeeBean
{
    /// <summary>订单类型。</summary>
    public enum IapOrderKind
    {
        /// <summary>已确认（可发货）。</summary>
        Confirmed,

        /// <summary>待处理（等待用户操作/商店处理，或等待服务器核销）。</summary>
        Pending,

        /// <summary>失败。</summary>
        Failed,

        /// <summary>延迟（如 App Store 家长同意）。</summary>
        Deferred,
    }

    /// <summary>
    /// 模块内部订单模型（由适配层从 Unity IAP 的 Order 转换而来）。
    /// </summary>
    public sealed class IapOrder
    {
        public IapOrderKind Kind;

        /// <summary>内部商品 ID。</summary>
        public string InternalId;

        /// <summary>交易 ID。</summary>
        public string TransactionId;

        /// <summary>收据（JSON）。</summary>
        public string Receipt;

        /// <summary>商店名（如 GooglePlay / AppleAppStore）。</summary>
        public string StoreName;

        /// <summary>失败原因（Kind=Failed 时）。</summary>
        public string FailureReason;

        /// <summary>失败/详情消息。</summary>
        public string Details;

        public override string ToString() => $"[{Kind}] {InternalId} txn={TransactionId} reason={FailureReason}";
    }
}

using System.Threading.Tasks;

namespace CoffeeBean
{
    /// <summary>服务器核销结果。</summary>
    public enum VerificationResult
    {
        /// <summary>服务器确认有效，可以发货。</summary>
        Verified,

        /// <summary>服务器判定无效（如重复/伪造收据）。</summary>
        Rejected,

        /// <summary>服务器请求出错（网络/超时/5xx），可重试。</summary>
        Error,
    }

    /// <summary>发送给服务器的核销载荷。</summary>
    public sealed class PurchasePayload
    {
        /// <summary>内部商品 ID。</summary>
        public string InternalId;

        /// <summary>当前平台商店 ID。</summary>
        public string PlatformProductId;

        /// <summary>交易 ID。</summary>
        public string TransactionId;

        /// <summary>收据（JSON，来自商店）。</summary>
        public string Receipt;

        /// <summary>商店名。</summary>
        public string StoreName;
    }

    /// <summary>
    /// 服务器二次确认接口（可选功能）。
    /// 未设置验证器 / 全局开关关闭时，购买直接完成，无需服务器。
    /// </summary>
    public interface IPurchaseVerifier
    {
        /// <summary>向服务器验证收据。实现方负责真正的 HTTP 调用。</summary>
        Task<VerificationResult> VerifyAsync(PurchasePayload payload);
    }
}

using System.Threading.Tasks;
using UnityEngine;

namespace CoffeeBean.Purchase.Samples
{
    /// <summary>
    /// 服务器二次确认示例实现：模拟 1.5 秒延迟后自动通过。
    /// 真实项目中把 VerifyAsync 换成对你们服务器的请求：
    /// 发送 payload（内部商品 ID / 平台商店 ID / 交易 ID / 收据 / 商店名），
    /// 服务器向 Google/Apple 校验收据后返回 Verified（有效）/ Rejected（无效）/ Error（可重试）。
    /// </summary>
    public sealed class DemoPurchaseVerifier : IPurchaseVerifier
    {
        /// <summary>模拟网络延迟（秒）。</summary>
        public float DelaySeconds = 1.5f;

        /// <summary>模拟服务器返回结果。</summary>
        public VerificationResult Result = VerificationResult.Verified;

        public async Task<VerificationResult> VerifyAsync(PurchasePayload payload)
        {
            Debug.Log($"[PurchaseDemo] → 发送服务器核销：商品={payload.InternalId} 平台ID={payload.PlatformProductId} txn={payload.TransactionId} 商店={payload.StoreName}");
            Debug.Log($"[PurchaseDemo]   收据: {payload.Receipt}");

            await Task.Delay((int)(DelaySeconds * 1000));

            Debug.Log($"[PurchaseDemo] ← 服务器返回: {Result}");
            return Result;
        }
    }
}

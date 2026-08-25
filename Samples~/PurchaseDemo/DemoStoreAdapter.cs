using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeBean.Purchase.Samples
{
    /// <summary>
    /// 编辑器演示用假商店：模拟商品下发 / 购买成功失败 / 恢复购买，
    /// 让"初始化 → 购买 → 发货 → 恢复"完整流程在编辑器里就能跑通。
    /// 真机 / 真商店请关闭 PurchaseDemo.useFakeStore，走 IapService.Instance 的真实商店。
    /// </summary>
    public sealed class DemoStoreAdapter : IIapStoreAdapter
    {
        public bool IsReady { get; private set; }

        /// <summary>置为 true 时下一次购买会模拟失败（演示失败回调）。</summary>
        public bool NextPurchaseFails;

        public event Action OnStoreConnected;
        public event Action<string> OnStoreDisconnected;
        public event Action<IReadOnlyList<IapProduct>> OnProductsFetched;
        public event Action<string> OnProductsFetchFailed;
        public event Action<IapOrder> OnPurchaseConfirmed;
        public event Action<IapOrder> OnPurchasePending;
        public event Action<IapOrder> OnPurchaseFailed;
        public event Action<IapOrder> OnPurchaseDeferred;
        public event Action<IReadOnlyList<IapOrder>> OnPurchasesFetched;
        public event Action<string> OnPurchasesFetchFailed;

        public void Connect()
        {
            IsReady = true;
            Debug.Log("[PurchaseDemo] 假商店已连接");
            OnStoreConnected?.Invoke();
        }

        public void FetchProducts(IReadOnlyList<IapProductDefinition> definitions)
        {
            var list = new List<IapProduct>();
            foreach (IapProductDefinition d in definitions)
            {
                if (d == null || !d.enabled) continue;
                list.Add(new IapProduct
                {
                    InternalId = d.internalId,
                    GoogleProductId = d.googleProductId,
                    AppleProductId = d.appleProductId,
                    ConsumeType = d.consumeType,
                    Available = true,
                    Title = "Demo " + d.internalId,
                    Description = "演示商品",
                    CurrencyCode = "CNY",
                    LocalizedPriceString = d.priceAnchor > 0 ? "¥" + d.priceAnchor : "¥6",
                    LocalizedPrice = (decimal)(d.priceAnchor > 0 ? d.priceAnchor : 6),
                });
            }
            OnProductsFetched?.Invoke(list);
        }

        public void Purchase(string internalId)
        {
            if (NextPurchaseFails)
            {
                NextPurchaseFails = false;
                OnPurchaseFailed?.Invoke(new IapOrder
                {
                    Kind = IapOrderKind.Failed,
                    InternalId = internalId,
                    FailureReason = "DemoFailure",
                    Details = "演示：模拟购买失败",
                });
                return;
            }

            OnPurchaseConfirmed?.Invoke(new IapOrder
            {
                Kind = IapOrderKind.Confirmed,
                InternalId = internalId,
                TransactionId = "demo-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Receipt = "{\"demo\":1,\"store\":\"fake\"}",
                StoreName = "DemoStore",
            });
        }

        public void ConfirmPendingPurchase(string transactionId)
        {
            Debug.Log("[PurchaseDemo] 假商店确认交易: " + transactionId);
        }

        public void RestorePurchases(Action<bool, string> onCompleted)
        {
            Debug.Log("[PurchaseDemo] 假商店恢复购买（演示直接成功）");
            onCompleted?.Invoke(true, "演示恢复完成（无历史购买）");
        }

        public void FetchPurchases() { }

        public void SetProcessPendingOrdersOnFetch(bool enabled) { }
    }
}

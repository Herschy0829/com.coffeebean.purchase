using System;
using System.Collections.Generic;

namespace CoffeeBean.Purchase.Tests
{
    /// <summary>
    /// 测试用假商店：脚本化行为，由测试驱动事件，验证 IapService 全流程。
    /// </summary>
    public sealed class FakeStoreAdapter : IIapStoreAdapter
    {
        public bool IsReady { get; private set; }
        public bool ProcessPendingOnFetch { get; private set; }

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

        /// <summary>记录被请求购买的商品。</summary>
        public readonly List<string> PurchasedInternalIds = new List<string>();

        /// <summary>记录被确认的待处理交易。</summary>
        public readonly List<string> ConfirmedTxnIds = new List<string>();

        public void Connect()
        {
            IsReady = true;
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
                    LocalizedPriceString = "$1.99",
                    CurrencyCode = "USD",
                });
            }
            OnProductsFetched?.Invoke(list);
        }

        public void Purchase(string internalId) => PurchasedInternalIds.Add(internalId);

        public void ConfirmPendingPurchase(string transactionId) => ConfirmedTxnIds.Add(transactionId);

        public void RestorePurchases(Action<bool, string> onCompleted) => onCompleted?.Invoke(true, "restored");

        public void FetchPurchases() { }

        public void SetProcessPendingOrdersOnFetch(bool enabled) => ProcessPendingOnFetch = enabled;

        // ===== 测试驱动 =====

        public void SimulateConfirmed(string internalId, string txnId, string receipt = "{\"fake\":1}")
            => OnPurchaseConfirmed?.Invoke(new IapOrder
            {
                Kind = IapOrderKind.Confirmed,
                InternalId = internalId,
                TransactionId = txnId,
                Receipt = receipt,
                StoreName = "FakeStore",
            });

        public void SimulateFailed(string internalId, string reason)
            => OnPurchaseFailed?.Invoke(new IapOrder
            {
                Kind = IapOrderKind.Failed,
                InternalId = internalId,
                FailureReason = reason,
            });

        public void SimulatePending(string internalId, string txnId)
            => OnPurchasePending?.Invoke(new IapOrder
            {
                Kind = IapOrderKind.Pending,
                InternalId = internalId,
                TransactionId = txnId,
            });

        public void SimulateHistoricalOrders(params IapOrder[] orders)
            => OnPurchasesFetched?.Invoke(orders);
    }
}

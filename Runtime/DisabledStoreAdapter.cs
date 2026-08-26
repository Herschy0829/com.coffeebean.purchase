using System;
using System.Collections.Generic;

namespace CoffeeBean
{
    /// <summary>
    /// 商店不可用时的兜底适配层（如编辑器/测试环境、商店工厂未注册）。
    /// 所有操作记录日志并返回失败，避免 IapService.Instance 创建时抛异常。
    /// </summary>
    public sealed class DisabledStoreAdapter : IIapStoreAdapter
    {
        public bool IsReady => false;

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
            IapLog.Error("Store unavailable (disabled adapter). Check Unity IAP initialization.");
        }

        public void FetchProducts(IReadOnlyList<IapProductDefinition> definitions)
            => OnProductsFetchFailed?.Invoke("Store unavailable (disabled adapter).");

        public void Purchase(string internalId) { }

        public void ConfirmPendingPurchase(string transactionId) { }

        public void RestorePurchases(Action<bool, string> onCompleted)
            => onCompleted?.Invoke(false, "Store unavailable (disabled adapter).");

        public void FetchPurchases() { }

        public void SetProcessPendingOrdersOnFetch(bool enabled) { }
    }
}

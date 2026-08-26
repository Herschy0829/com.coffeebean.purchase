using System;
using System.Collections.Generic;

namespace CoffeeBean
{
    /// <summary>
    /// 商店适配层抽象：把 Unity IAP 5.4 隔离在实现里（UnityIapStoreAdapter），
    /// 其余代码只依赖本接口；测试用 FakeStoreAdapter 即可验证全流程。
    /// </summary>
    public interface IIapStoreAdapter
    {
        /// <summary>是否已连接商店。</summary>
        bool IsReady { get; }

        /// <summary>商店连接成功。</summary>
        event Action OnStoreConnected;

        /// <summary>商店连接断开（含失败原因）。</summary>
        event Action<string> OnStoreDisconnected;

        /// <summary>商品下发完成（缓存数据已就绪）。</summary>
        event Action<IReadOnlyList<IapProduct>> OnProductsFetched;

        /// <summary>商品下发失败（原因）。</summary>
        event Action<string> OnProductsFetchFailed;

        /// <summary>购买已确认（可发货/核销）。</summary>
        event Action<IapOrder> OnPurchaseConfirmed;

        /// <summary>购买待处理（等待用户/商店，或等待服务器核销）。</summary>
        event Action<IapOrder> OnPurchasePending;

        /// <summary>购买失败。</summary>
        event Action<IapOrder> OnPurchaseFailed;

        /// <summary>购买被延迟（如家长同意）。</summary>
        event Action<IapOrder> OnPurchaseDeferred;

        /// <summary>历史购买已拉取（补发用）。</summary>
        event Action<IReadOnlyList<IapOrder>> OnPurchasesFetched;

        /// <summary>历史购买拉取失败。</summary>
        event Action<string> OnPurchasesFetchFailed;

        /// <summary>连接商店。</summary>
        void Connect();

        /// <summary>拉取商品（定义来自 Excel 配置）。</summary>
        void FetchProducts(IReadOnlyList<IapProductDefinition> definitions);

        /// <summary>发起购买（按内部 ID）。</summary>
        void Purchase(string internalId);

        /// <summary>确认一笔待处理订单（服务器核销通过后调用）。</summary>
        void ConfirmPendingPurchase(string transactionId);

        /// <summary>恢复购买（完成回调 success + message）。</summary>
        void RestorePurchases(Action<bool, string> onCompleted);

        /// <summary>拉取历史购买（补发/去重用）。</summary>
        void FetchPurchases();

        /// <summary>是否在拉取历史购买时自动处理待处理订单（补发）。</summary>
        void SetProcessPendingOrdersOnFetch(bool enabled);
    }
}

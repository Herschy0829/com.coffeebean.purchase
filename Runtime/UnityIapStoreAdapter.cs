using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace CoffeeBean
{
    /// <summary>
    /// Unity IAP 5.4 适配层：本文件是模块里唯一直接引用 UnityEngine.Purchasing 的地方。
    /// 负责把 StoreController 的事件/模型映射为模块内部的 IIapStoreAdapter 契约。
    /// </summary>
    public sealed class UnityIapStoreAdapter : IIapStoreAdapter
    {
        private readonly StoreController _controller;
        private readonly Dictionary<string, PendingOrder> _pendingByTxn = new Dictionary<string, PendingOrder>();
        private readonly Dictionary<string, IapProductDefinition> _defsById = new Dictionary<string, IapProductDefinition>(StringComparer.OrdinalIgnoreCase);
        private Action<bool, string> _restoreCallback;

        public UnityIapStoreAdapter()
        {
            _controller = UnityIAPServices.StoreController();
            WireEvents();
        }

        private void WireEvents()
        {
            _controller.OnStoreConnected += () =>
            {
                IapLog.Log("Store connected.");
                OnStoreConnected?.Invoke();
            };
            _controller.OnStoreDisconnected += _ => IapLog.Warn("Store disconnected.");
            _controller.OnProductsFetched += HandleProductsFetched;
            _controller.OnProductsFetchFailed += f => OnProductsFetchFailed?.Invoke(f.FailureReason);
            _controller.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            _controller.OnPurchasePending += HandlePurchasePending;
            _controller.OnPurchaseFailed += HandlePurchaseFailed;
            _controller.OnPurchaseDeferred += HandlePurchaseDeferred;
            _controller.OnPurchasesFetched += HandlePurchasesFetched;
            _controller.OnPurchasesFetchFailed += f => OnPurchasesFetchFailed?.Invoke(f.Message);
        }

        public bool IsReady { get; private set; }

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
            _controller.Connect().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    IapLog.Error("Store Connect failed: " + t.Exception);
                    OnStoreDisconnected?.Invoke(t.Exception?.Message ?? "Connect failed");
                }
            });
        }

        public void FetchProducts(IReadOnlyList<IapProductDefinition> definitions)
        {
            _defsById.Clear();
            var list = new List<ProductDefinition>();
            foreach (var d in definitions)
            {
                if (d == null || !d.enabled) continue;
                _defsById[d.internalId] = d;
                list.Add(new ProductDefinition(d.internalId, IapPlatform.ResolveStoreId(d), ToProductType(d.consumeType), true));
            }
            _controller.FetchProducts(list, new NoRetriesPolicy());
        }

        public void Purchase(string internalId)
        {
            Product product = _controller.GetProductById(internalId);
            if (product == null)
            {
                IapLog.Error("Product not found: " + internalId);
                OnPurchaseFailed?.Invoke(new IapOrder
                {
                    Kind = IapOrderKind.Failed,
                    InternalId = internalId,
                    FailureReason = nameof(PurchaseFailureReason.ProductUnavailable),
                    Details = "Product not found after initialization",
                });
                return;
            }
            _controller.PurchaseProduct(product);
        }

        public void ConfirmPendingPurchase(string transactionId)
        {
            if (_pendingByTxn.TryGetValue(transactionId, out PendingOrder pending))
            {
                _pendingByTxn.Remove(transactionId);
                _controller.ConfirmPurchase(pending);
            }
            else
            {
                IapLog.Warn("No pending order found for transaction: " + transactionId);
            }
        }

        public void RestorePurchases(Action<bool, string> onCompleted)
        {
            _restoreCallback = onCompleted;
            _controller.RestoreTransactions((ok, message) =>
            {
                var cb = _restoreCallback;
                _restoreCallback = null;
                cb?.Invoke(ok, message ?? string.Empty);
            });
        }

        public void FetchPurchases()
        {
            _controller.FetchPurchases();
        }

        public void SetProcessPendingOrdersOnFetch(bool enabled)
        {
            _controller.ProcessPendingOrdersOnPurchasesFetched(enabled);
        }

        private void HandleProductsFetched(List<Product> products)
        {
            var result = new List<IapProduct>();
            foreach (Product p in products)
            {
                var def = p.definition != null && _defsById.TryGetValue(p.definition.id, out var d) ? d : null;
                result.Add(new IapProduct
                {
                    InternalId = p.definition?.id,
                    GoogleProductId = def?.googleProductId ?? string.Empty,
                    AppleProductId = def?.appleProductId ?? string.Empty,
                    ConsumeType = def != null ? def.consumeType : IapConsumeType.Consumable,
                    Available = p.availableToPurchase,
                    Title = p.metadata?.localizedTitle ?? string.Empty,
                    Description = p.metadata?.localizedDescription ?? string.Empty,
                    CurrencyCode = p.metadata?.isoCurrencyCode ?? string.Empty,
                    LocalizedPriceString = p.metadata?.localizedPriceString ?? string.Empty,
                    LocalizedPrice = p.metadata?.localizedPrice ?? 0m,
                    // 注：v5 中收据/交易 ID 从 Order（IOrderInfo）获取，不从 Product 缓存取
                });
            }
            IsReady = true;
            OnProductsFetched?.Invoke(result);
        }

        private void HandlePurchaseConfirmed(Order order) => OnPurchaseConfirmed?.Invoke(MapOrder(order, IapOrderKind.Confirmed));

        private void HandlePurchasePending(PendingOrder order)
        {
            if (order.Info != null && !string.IsNullOrEmpty(order.Info.TransactionID))
                _pendingByTxn[order.Info.TransactionID] = order;
            OnPurchasePending?.Invoke(MapOrder(order, IapOrderKind.Pending));
        }

        private void HandlePurchaseFailed(FailedOrder order) => OnPurchaseFailed?.Invoke(MapOrder(order, IapOrderKind.Failed));

        private void HandlePurchaseDeferred(DeferredOrder order) => OnPurchaseDeferred?.Invoke(MapOrder(order, IapOrderKind.Deferred));

        private void HandlePurchasesFetched(Orders orders)
        {
            var list = new List<IapOrder>();
            if (orders.ConfirmedOrders != null)
                foreach (var o in orders.ConfirmedOrders) list.Add(MapOrder(o, IapOrderKind.Confirmed));
            if (orders.PendingOrders != null)
                foreach (var o in orders.PendingOrders) list.Add(MapOrder(o, IapOrderKind.Pending));
            if (orders.DeferredOrders != null)
                foreach (var o in orders.DeferredOrders) list.Add(MapOrder(o, IapOrderKind.Deferred));
            OnPurchasesFetched?.Invoke(list);
        }

        private static IapOrder MapOrder(Order order, IapOrderKind kind)
        {
            string internalId = string.Empty;
            var items = order?.CartOrdered?.Items();
            if (items != null && items.Count > 0 && items[0].Product != null)
                internalId = items[0].Product.definition?.id ?? string.Empty;

            var result = new IapOrder
            {
                Kind = kind,
                InternalId = internalId,
                TransactionId = order?.Info?.TransactionID ?? string.Empty,
                Receipt = order?.Info?.Receipt ?? string.Empty,
                // IOrderInfo 不直接暴露 StoreName，服务器核销时按平台推断（见 IapService）
            };

            if (order is FailedOrder failed)
            {
                result.FailureReason = failed.FailureReason.ToString();
                result.Details = failed.Details;
            }
            return result;
        }

        private static ProductType ToProductType(IapConsumeType consumeType)
        {
            switch (consumeType)
            {
                case IapConsumeType.NonConsumable: return ProductType.NonConsumable;
                case IapConsumeType.Subscription: return ProductType.Subscription;
                default: return ProductType.Consumable;
            }
        }
    }
}

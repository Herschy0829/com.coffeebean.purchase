using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CoffeeBean.Purchase
{
    /// <summary>
    /// 支付模块对外门面：初始化、商品缓存查询、购买（可选服务器二次确认）、恢复购买。
    /// 通过 IIapStoreAdapter 与商店解耦；默认使用 UnityIapStoreAdapter（Unity IAP 5.4）。
    /// </summary>
    public sealed class IapService
    {
        private readonly IIapStoreAdapter _adapter;
        private readonly PurchaseJournal _journal = new PurchaseJournal();
        private readonly Dictionary<string, IapProduct> _byInternal = new Dictionary<string, IapProduct>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _byGoogle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _byApple = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private IapConfig _config;
        private IPurchaseVerifier _verifier;
        private bool _initializing;
        private bool _initialized;
        private string _purchasingInternalId;

        /// <summary>
        /// 默认实例（使用真实商店适配层）。惰性创建：编辑器/测试环境商店工厂未注册时
        /// 回退到 DisabledStoreAdapter，避免类型初始化抛异常。集成模式可由 Core 注册，独立模式直接使用。
        /// </summary>
        public static IapService Instance
        {
            get
            {
                if (_instance == null)
                {
                    try
                    {
                        _instance = new IapService(new UnityIapStoreAdapter());
                    }
                    catch (Exception e)
                    {
                        IapLog.Warn("Unity store adapter unavailable, using disabled adapter: " + e.Message);
                        _instance = new IapService(new DisabledStoreAdapter());
                    }
                }
                return _instance;
            }
        }

        private static IapService _instance;

        public IapService(IIapStoreAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            WireAdapterEvents();
        }

        public bool IsInitialized => _initialized;
        public bool IsPurchasingInProgress => _purchasingInternalId != null;
        public IapConfig Config => _config;

        /// <summary>初始化完成（商品已下发缓存）。</summary>
        public event Action OnInitialized;

        /// <summary>初始化失败（原因）。</summary>
        public event Action<string> OnInitFailed;

        /// <summary>单个商品缓存更新（下发/变化）。</summary>
        public event Action<IapProduct> OnProductUpdated;

        /// <summary>购买确认并已发货（业务层在此发放权益）。</summary>
        public event Action<IapOrder> OnPurchaseSucceeded;

        /// <summary>购买失败（含原因）。</summary>
        public event Action<IapOrder> OnPurchaseFailed;

        /// <summary>购买待处理（等待用户操作/商店处理）。</summary>
        public event Action<IapOrder> OnPurchasePending;

        /// <summary>购买被延迟（如家长同意）。</summary>
        public event Action<IapOrder> OnPurchaseDeferred;

        /// <summary>恢复购买完成（success + message）。</summary>
        public event Action<bool, string> OnRestoreFinished;

        /// <summary>恢复购买失败（原因）。</summary>
        public event Action<string> OnRestoreFailed;

        /// <summary>补发：历史购买重新处理完成（崩溃/断网恢复）。</summary>
        public event Action<IapOrder> OnPendingPurchaseReprocessed;

        // ========== 生命周期 ==========

        /// <summary>
        /// 初始化：连接商店 → 拉取商品 → 拉取历史购买（补发）。
        /// </summary>
        /// <param name="config">Excel 生成的配置；为空时尝试 Resources.Load("CoffeeBean/IapConfig")。</param>
        /// <param name="verifier">服务器二次确认实现（可选；为空或全局关闭时直接完成购买）。</param>
        public void Initialize(IapConfig config, IPurchaseVerifier verifier = null)
        {
            if (_initialized || _initializing) return;

            _config = config ?? Resources.Load<IapConfig>("CoffeeBean/IapConfig");
            if (_config == null)
            {
                OnInitFailed?.Invoke("IapConfig not found. Generate it via Window > CoffeeBean > Purchase Config.");
                return;
            }
            _verifier = verifier;
            _initializing = true;

            _adapter.SetProcessPendingOrdersOnFetch(true); // 补发：拉取历史购买时自动处理待处理订单
            _adapter.Connect();

            if (_adapter is DisabledStoreAdapter)
            {
                _initializing = false;
                OnInitFailed?.Invoke("Unity IAP store is not available (store factory missing).");
            }
        }

        // ========== 商品查询 ==========

        /// <summary>按内部 ID 获取商品缓存。</summary>
        public IapProduct GetProduct(string internalId)
            => internalId != null && _byInternal.TryGetValue(internalId, out var p) ? p : null;

        /// <summary>通过 Google 平台 ID 获取商品缓存。</summary>
        public IapProduct GetProductByGoogleId(string googleId)
            => googleId != null && _byGoogle.TryGetValue(googleId, out string id) ? GetProduct(id) : null;

        /// <summary>通过 Apple 平台 ID 获取商品缓存。</summary>
        public IapProduct GetProductByAppleId(string appleId)
            => appleId != null && _byApple.TryGetValue(appleId, out string id) ? GetProduct(id) : null;

        /// <summary>通过当前平台 ID 获取商品缓存（Google 或 Apple 任一匹配）。</summary>
        public IapProduct GetProductByPlatformId(string platformId)
            => GetProductByGoogleId(platformId) ?? GetProductByAppleId(platformId);

        // ========== 购买 ==========

        /// <summary>发起购买（内部 ID）。防重入：一次只允许一笔进行中的购买。</summary>
        public void Purchase(string internalId)
        {
            if (!_initialized)
            {
                OnPurchaseFailed?.Invoke(FailOrder(internalId, "StoreNotInitialized", "Store is not initialized yet."));
                return;
            }
            if (_purchasingInternalId != null)
            {
                OnPurchaseFailed?.Invoke(FailOrder(internalId, "PurchaseInProgress",
                    $"Another purchase ({_purchasingInternalId}) is already in progress."));
                return;
            }
            IapProduct product = GetProduct(internalId);
            if (product == null || !product.Available)
            {
                OnPurchaseFailed?.Invoke(FailOrder(internalId, "ProductUnavailable",
                    product == null ? "Product not in cache." : "Product is not available to purchase."));
                return;
            }

            _purchasingInternalId = internalId;
            IapLog.Log($"Purchase started: {internalId}");
            _adapter.Purchase(internalId);
        }

        /// <summary>恢复购买（Apple 恢复交易 / Google 非消耗品恢复）。</summary>
        public void RestorePurchases()
        {
            if (!_initialized)
            {
                OnRestoreFailed?.Invoke("Store is not initialized yet.");
                return;
            }
            _adapter.RestorePurchases((ok, message) =>
            {
                if (ok) OnRestoreFinished?.Invoke(true, message);
                else OnRestoreFailed?.Invoke(message);
            });
        }

        // ========== 内部：适配层事件 ==========

        private void WireAdapterEvents()
        {
            _adapter.OnStoreConnected += OnStoreConnectedHandler;
            _adapter.OnProductsFetched += OnProductsFetched;
            _adapter.OnProductsFetchFailed += reason =>
            {
                _initializing = false;
                OnInitFailed?.Invoke("Fetch products failed: " + reason);
            };
            _adapter.OnPurchaseConfirmed += OnPurchaseConfirmed;
            _adapter.OnPurchasePending += order => OnPurchasePending?.Invoke(order);
            _adapter.OnPurchaseFailed += order =>
            {
                _purchasingInternalId = null;
                OnPurchaseFailed?.Invoke(order);
            };
            _adapter.OnPurchaseDeferred += order =>
            {
                _purchasingInternalId = null;
                OnPurchaseDeferred?.Invoke(order);
            };
            _adapter.OnPurchasesFetched += OnPurchasesFetched;
            _adapter.OnPurchasesFetchFailed += message => IapLog.Warn("Fetch purchases failed: " + message);
        }

        private void OnStoreConnectedHandler()
        {
            if (_config == null) return;
            IapLog.Log("Store connected, fetching products and purchases...");
            _adapter.FetchProducts(_config.products);
            _adapter.FetchPurchases(); // 拉取历史购买（补发）
        }

        private void OnProductsFetched(IReadOnlyList<IapProduct> products)
        {
            foreach (IapProduct p in products)
            {
                _byInternal[p.InternalId] = p;
                if (!string.IsNullOrEmpty(p.GoogleProductId)) _byGoogle[p.GoogleProductId] = p.InternalId;
                if (!string.IsNullOrEmpty(p.AppleProductId)) _byApple[p.AppleProductId] = p.InternalId;
                OnProductUpdated?.Invoke(p);
            }
            _initializing = false;
            _initialized = true;
            IapLog.Log($"Products fetched: {products.Count}, service initialized.");
            OnInitialized?.Invoke();
        }

        private void OnPurchaseConfirmed(IapOrder order)
        {
            _purchasingInternalId = null;

            if (_journal.Contains(order.TransactionId))
            {
                IapLog.Log($"Purchase already processed, skipped (txn={order.TransactionId}).");
                return;
            }

            if (ShouldServerVerify(order.InternalId))
            {
                VerifyAndFulfill(order);
            }
            else
            {
                Fulfill(order);
            }
        }

        private void OnPurchasesFetched(IReadOnlyList<IapOrder> orders)
        {
            foreach (IapOrder order in orders)
            {
                switch (order.Kind)
                {
                    case IapOrderKind.Confirmed:
                        // 补发：上次未处理完的历史购买
                        if (_journal.Contains(order.TransactionId)) continue;
                        IapLog.Log($"Reprocessing pending purchase: {order}");
                        if (ShouldServerVerify(order.InternalId)) VerifyAndFulfill(order);
                        else Fulfill(order);
                        OnPendingPurchaseReprocessed?.Invoke(order);
                        break;

                    case IapOrderKind.Pending:
                        OnPurchasePending?.Invoke(order);
                        break;

                    case IapOrderKind.Deferred:
                        OnPurchaseDeferred?.Invoke(order);
                        break;
                }
            }
        }

        // ========== 内部：核销 ==========

        private bool ShouldServerVerify(string internalId)
        {
            if (_config == null || !_config.serverVerifyEnabled) return false;
            IapProductDefinition def = _config.FindByInternalId(internalId);
            return def == null || def.serverVerifyOverride != 0; // -1 跟随全局（开）；0 显式关闭；1 显式开启
        }

        private void Fulfill(IapOrder order)
        {
            _journal.Add(order.TransactionId);
            IapLog.Log($"Fulfilled: {order.InternalId} txn={order.TransactionId}");
            OnPurchaseSucceeded?.Invoke(order);
        }

        private async void VerifyAndFulfill(IapOrder order)
        {
            if (_verifier == null)
            {
                IapLog.Warn($"Server verify enabled but no IPurchaseVerifier set; completing purchase directly: {order.InternalId}");
                Fulfill(order);
                return;
            }

            var payload = new PurchasePayload
            {
                InternalId = order.InternalId,
                PlatformProductId = GetProduct(order.InternalId)?.PlatformId ?? string.Empty,
                TransactionId = order.TransactionId,
                Receipt = order.Receipt,
                StoreName = IapPlatform.IsApple ? "AppleAppStore" : "GooglePlay",
            };

            int retries = Mathf.Max(0, _config.verifyRetryCount);
            for (int attempt = 0; attempt <= retries; attempt++)
            {
                VerificationResult result = VerificationResult.Error;
                bool completed = false;
                try
                {
                    Task<VerificationResult> task = _verifier.VerifyAsync(payload);
                    Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(_config.verifyTimeoutSeconds)));
                    completed = finished == task;
                    if (completed) result = task.Result;
                }
                catch (Exception e)
                {
                    IapLog.Warn($"Verifier threw (attempt {attempt + 1}/{retries + 1}): {e.Message}");
                }

                if (completed && result == VerificationResult.Verified)
                {
                    Fulfill(order);
                    return;
                }
                if (completed && result == VerificationResult.Rejected)
                {
                    IapLog.Warn($"Server rejected receipt: {order.TransactionId}. Purchase kept unfulfilled.");
                    return; // 不再自动补发（服务器判定无效）；如需人工处理可监听日志/单独接口
                }
                IapLog.Warn($"Server verify failed/timeout (attempt {attempt + 1}/{retries + 1}), will retry.");
            }

            // 重试耗尽：保持未核销。商店侧交易未确认 → 下次启动 FetchPurchases 时再次补发
            IapLog.Warn($"Server verify exhausted retries, purchase kept pending for later re-process: {order.TransactionId}");
        }

        private static IapOrder FailOrder(string internalId, string reason, string details)
            => new IapOrder
            {
                Kind = IapOrderKind.Failed,
                InternalId = internalId,
                FailureReason = reason,
                Details = details,
            };
    }
}

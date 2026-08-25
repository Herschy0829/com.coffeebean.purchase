using System.Collections.Generic;
using UnityEngine;

namespace CoffeeBean.Purchase.Samples
{
    /// <summary>
    /// 完整购买流程演示：
    ///   初始化（连接商店）→ 商品下发缓存 → 查询商品 → 购买（可选服务器二次确认）→ 发货 → 失败处理 → 恢复购买。
    ///
    /// 使用：
    ///   1. 先生成配置（Window > CoffeeBean > Purchase Config）
    ///   2. 场景中新建空物体，挂上本组件（默认开假商店，编辑器里即可跑通全流程）
    ///   3. 运行后按界面按钮操作；Console 里看完整日志
    /// </summary>
    public sealed class PurchaseDemo : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("留空则从 Resources/CoffeeBean/IapConfig 自动加载")]
        [SerializeField] private IapConfig config;

        [Tooltip("编辑器演示用假商店（真机请关闭，走真实商店 IapService.Instance）")]
        [SerializeField] private bool useFakeStore = true;

        [Tooltip("演示服务器二次确认：开启后购买会先等待核销（DemoPurchaseVerifier 模拟延迟），通过后才发货")]
        [SerializeField] private bool demoServerVerify;

        [Tooltip("演示购买的商品 ID（来自配置表 Id_s）")]
        [SerializeField] private string purchaseProductId = "gem_100";

        private IapService _service;
        private string _status = "未初始化";
        private readonly List<string> _log = new List<string>();
        private Vector2 _scroll;
        private bool _subscribed;

        private void Start()
        {
            // 1. 构建服务：假商店（编辑器演示）或真实商店（真机）
            IIapStoreAdapter adapter = useFakeStore ? (IIapStoreAdapter)new DemoStoreAdapter() : null;
            _service = adapter != null ? new IapService(adapter) : IapService.Instance;
            // 集成模式（装了 Core）也可以这样拿共享实例：
            //   var _service = CoffeeBeanBootstrapper.Context.Services.Get<IapService>();

            // 2. 加载配置（Excel 生成产物）
            IapConfig cfg = config != null ? config : Resources.Load<IapConfig>("CoffeeBean/IapConfig");
            if (cfg == null)
            {
                _status = "未找到 IapConfig！请先在 Window > CoffeeBean > Purchase Config 生成配置";
                Log("错误: 缺少 IapConfig（Assets/Resources/CoffeeBean/IapConfig.asset）");
                return;
            }

            // 3. 服务器核销器（可选）：演示用自动通过，真实项目换成你自己的 IPurchaseVerifier
            IPurchaseVerifier verifier = demoServerVerify ? new DemoPurchaseVerifier() : null;

            // 4. 订阅完整流程的所有回调
            SubscribeEvents();

            // 5. 初始化：连接商店 → 商品下发 → 拉取历史购买（补发）
            _status = "初始化中...";
            Log("开始初始化...");
            _service.Initialize(cfg, verifier);
        }

        private void OnDestroy()
        {
            // 最佳实践：成对订阅/退订，防止事件持有本对象导致泄漏
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            _subscribed = true;
            _service.OnInitialized += OnInitialized;
            _service.OnInitFailed += OnInitFailed;
            _service.OnProductUpdated += OnProductUpdated;
            _service.OnPurchaseSucceeded += OnPurchaseSucceeded;
            _service.OnPurchaseFailed += OnPurchaseFailed;
            _service.OnPurchasePending += OnPurchasePending;
            _service.OnPurchaseDeferred += OnPurchaseDeferred;
            _service.OnRestoreFinished += OnRestoreFinished;
            _service.OnRestoreFailed += OnRestoreFailed;
            _service.OnPendingPurchaseReprocessed += OnReprocessed;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            _subscribed = false;
            _service.OnInitialized -= OnInitialized;
            _service.OnInitFailed -= OnInitFailed;
            _service.OnProductUpdated -= OnProductUpdated;
            _service.OnPurchaseSucceeded -= OnPurchaseSucceeded;
            _service.OnPurchaseFailed -= OnPurchaseFailed;
            _service.OnPurchasePending -= OnPurchasePending;
            _service.OnPurchaseDeferred -= OnPurchaseDeferred;
            _service.OnRestoreFinished -= OnRestoreFinished;
            _service.OnRestoreFailed -= OnRestoreFailed;
            _service.OnPendingPurchaseReprocessed -= OnReprocessed;
        }

        // ========== 流程回调 ==========

        private void OnInitialized()
        {
            _status = "初始化完成，商品已下发缓存";
            Log("初始化完成");
        }

        private void OnInitFailed(string reason)
        {
            _status = "初始化失败: " + reason;
            Log("初始化失败: " + reason);
        }

        private void OnProductUpdated(IapProduct p)
        {
            Log($"商品下发: {p.InternalId} 价格={p.LocalizedPriceString} 货币={p.CurrencyCode} 可用={p.Available} 平台ID={p.PlatformId}");
        }

        private void OnPurchaseSucceeded(IapOrder order)
        {
            _status = "购买成功，已发货: " + order.InternalId;
            Log($"【发货】{order.InternalId} txn={order.TransactionId} → 在这里发放你的游戏权益");
        }

        private void OnPurchaseFailed(IapOrder order)
        {
            _status = "购买失败: " + order.FailureReason;
            Log($"购买失败: {order.InternalId} 原因={order.FailureReason} 详情={order.Details}");
        }

        private void OnPurchasePending(IapOrder order)
        {
            _status = "购买待处理（等待商店/用户操作）: " + order.InternalId;
            Log($"购买 Pending: {order.InternalId}");
        }

        private void OnPurchaseDeferred(IapOrder order)
        {
            _status = "购买被延迟（如家长同意）";
            Log($"购买 Deferred: {order.InternalId}");
        }

        private void OnRestoreFinished(bool ok, string message)
        {
            _status = "恢复购买完成";
            Log($"恢复购买完成: {message}");
        }

        private void OnRestoreFailed(string reason)
        {
            _status = "恢复购买失败";
            Log("恢复购买失败: " + reason);
        }

        private void OnReprocessed(IapOrder order)
        {
            Log($"【补发】历史购买重新处理: {order.InternalId} txn={order.TransactionId}");
        }

        // ========== 界面（IMGUI，无需任何预制体/UI 资源）==========

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 480, Screen.height - 20));
            GUILayout.Label("<b>CoffeeBean Purchase Demo</b>", GUILayout.Height(22));

            GUI.color = _service != null && _service.IsInitialized ? Color.green : Color.yellow;
            GUILayout.Label("状态: " + _status);
            GUI.color = Color.white;

            if (_service != null && _service.IsInitialized)
            {
                DrawProducts();
                GUILayout.Space(6);
                GUILayout.Label("购买 ID: " + purchaseProductId);
                if (GUILayout.Button("购买 " + purchaseProductId, GUILayout.Height(36)))
                    _service.Purchase(purchaseProductId);
                if (GUILayout.Button("恢复购买 (Restore)", GUILayout.Height(36)))
                    _service.RestorePurchases();
            }

            GUILayout.Space(10);
            GUILayout.Label("日志（最近 15 条）:");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            int start = Mathf.Max(0, _log.Count - 15);
            for (int i = start; i < _log.Count; i++)
                GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawProducts()
        {
            if (_service.Config == null) return;
            GUILayout.Label("商品缓存（价格来自商店下发）:");
            foreach (IapProductDefinition def in _service.Config.products)
            {
                IapProduct p = _service.GetProduct(def.internalId);
                if (p == null) continue;
                GUILayout.Label($"  {p.InternalId}  {p.LocalizedPriceString}  {p.CurrencyCode}  {(p.Available ? "可购买" : "不可用")}");
            }
        }

        private void Log(string message)
        {
            Debug.Log("[PurchaseDemo] " + message);
            _log.Add(message);
            while (_log.Count > 30) _log.RemoveAt(0);
        }
    }
}

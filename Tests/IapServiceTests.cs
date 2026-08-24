using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoffeeBean.Purchase;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeBean.Purchase.Tests
{
    /// <summary>
    /// IapService 全流程测试：初始化缓存、购买（无服务器/有服务器核销/重试/拒绝）、
    /// 防重入、去重、恢复购买、历史购买补发。
    /// </summary>
    public class IapServiceTests
    {
        private const string JournalKey = "CoffeeBean.Purchase.Journal";

        private FakeStoreAdapter _adapter;
        private IapService _service;
        private IapConfig _config;
        private readonly List<IapOrder> _succeeded = new List<IapOrder>();
        private readonly List<IapOrder> _failed = new List<IapOrder>();
        private readonly List<IapOrder> _reprocessed = new List<IapOrder>();
        private bool _initialized;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(JournalKey);
            _adapter = new FakeStoreAdapter();
            _service = new IapService(_adapter);
            _config = CreateConfig();
            _succeeded.Clear();
            _failed.Clear();
            _reprocessed.Clear();
            _initialized = false;

            _service.OnInitialized += () => _initialized = true;
            _service.OnPurchaseSucceeded += o => _succeeded.Add(o);
            _service.OnPurchaseFailed += o => _failed.Add(o);
            _service.OnPendingPurchaseReprocessed += o => _reprocessed.Add(o);
        }

        private static IapConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<IapConfig>();
            config.serverVerifyEnabled = false;
            config.verifyRetryCount = 2;
            config.verifyTimeoutSeconds = 1f;
            config.products.Add(new IapProductDefinition
            {
                internalId = "gem_100", googleProductId = "com.example.gem100", appleProductId = "com.example.gem100.ios",
                consumeType = IapConsumeType.Consumable, enabled = true,
            });
            config.products.Add(new IapProductDefinition
            {
                internalId = "no_ads", googleProductId = "com.example.noads", appleProductId = "com.example.noads.ios",
                consumeType = IapConsumeType.NonConsumable, enabled = true,
            });
            return config;
        }

        private void Init()
        {
            _service.Initialize(_config, null);
            Assert.IsTrue(_initialized, "应在初始化后触发 OnInitialized");
        }

        [Test]
        public void Initialize_CachesProductsByAllIds()
        {
            Init();
            Assert.AreEqual("gem_100", _service.GetProduct("gem_100").InternalId);
            Assert.AreEqual("gem_100", _service.GetProductByGoogleId("com.example.gem100").InternalId);
            Assert.AreEqual("gem_100", _service.GetProductByAppleId("com.example.gem100.ios").InternalId);
            Assert.AreEqual("gem_100", _service.GetProductByPlatformId("com.example.gem100.ios").InternalId);
            Assert.IsNull(_service.GetProduct("nope"));
        }

        [Test]
        public void Purchase_NoServerVerify_FulfillsImmediately()
        {
            Init();
            _service.Purchase("gem_100");
            _adapter.SimulateConfirmed("gem_100", "txn-1");
            Assert.AreEqual(1, _succeeded.Count);
            Assert.AreEqual("txn-1", _succeeded[0].TransactionId);
        }

        [Test]
        public void Purchase_DuplicateTxn_Skipped()
        {
            Init();
            _service.Purchase("gem_100");
            _adapter.SimulateConfirmed("gem_100", "txn-dup");
            _adapter.SimulateConfirmed("gem_100", "txn-dup"); // 崩溃重入
            Assert.AreEqual(1, _succeeded.Count);
        }

        [Test]
        public void Purchase_SecondWhileInProgress_Fails()
        {
            Init();
            _service.Purchase("gem_100");
            _service.Purchase("no_ads"); // 第一笔未确认
            Assert.AreEqual(1, _failed.Count);
            Assert.IsTrue(_failed[0].FailureReason.Contains("InProgress"), "应拒绝进行中的第二笔购买");
        }

        [Test]
        public void Purchase_NotInitialized_Fails()
        {
            _service.Purchase("gem_100");
            Assert.AreEqual(1, _failed.Count);
            Assert.IsTrue(_failed[0].FailureReason.Contains("NotInitialized"));
        }

        [Test]
        public void Purchase_ServerVerify_CompletesAfterVerifierConfirms()
        {
            _config.serverVerifyEnabled = true;
            _service.Initialize(_config, new FakeVerifier(VerificationResult.Verified));
            Assert.IsTrue(_initialized);

            _service.Purchase("gem_100");
            _adapter.SimulateConfirmed("gem_100", "txn-v");

            Assert.IsTrue(WaitFor(() => _succeeded.Count == 1));
            Assert.AreEqual("txn-v", _succeeded[0].TransactionId);
        }

        [Test]
        public void Purchase_ServerVerify_Rejected_NotFulfilled()
        {
            _config.serverVerifyEnabled = true;
            _service.Initialize(_config, new FakeVerifier(VerificationResult.Rejected));
            Assert.IsTrue(_initialized);

            _service.Purchase("gem_100");
            _adapter.SimulateConfirmed("gem_100", "txn-r");

            Assert.IsFalse(WaitFor(() => _succeeded.Count == 1, 300), "服务器拒绝不应发货");
            Assert.AreEqual(0, _succeeded.Count);
        }

        [Test]
        public void Purchase_ServerVerify_RetriesThenSucceeds()
        {
            _config.serverVerifyEnabled = true;
            _service.Initialize(_config, new FakeVerifier(VerificationResult.Error, VerificationResult.Verified));
            Assert.IsTrue(_initialized);

            _service.Purchase("gem_100");
            _adapter.SimulateConfirmed("gem_100", "txn-t");

            Assert.IsTrue(WaitFor(() => _succeeded.Count == 1, 5000), "第 2 次尝试应核销成功");
            Assert.AreEqual(1, _succeeded.Count);
        }

        [Test]
        public void RestorePurchases_ReportsFinished()
        {
            Init();
            bool finished = false;
            _service.OnRestoreFinished += (ok, msg) => finished = ok;
            _service.RestorePurchases();
            Assert.IsTrue(finished);
        }

        [Test]
        public void FetchPurchases_ReprocessesHistoricalOrders()
        {
            Init();
            _adapter.SimulateHistoricalOrders(
                new IapOrder { Kind = IapOrderKind.Confirmed, InternalId = "no_ads", TransactionId = "txn-hist" });

            Assert.AreEqual(1, _reprocessed.Count, "历史购买应触发补发回调");
            Assert.AreEqual(1, _succeeded.Count);
        }

        [Test]
        public void FetchPurchases_DuplicateHistorical_Skipped()
        {
            Init();
            _adapter.SimulateHistoricalOrders(
                new IapOrder { Kind = IapOrderKind.Confirmed, InternalId = "no_ads", TransactionId = "txn-h2" });
            _adapter.SimulateHistoricalOrders(
                new IapOrder { Kind = IapOrderKind.Confirmed, InternalId = "no_ads", TransactionId = "txn-h2" });

            Assert.AreEqual(1, _succeeded.Count, "重复的历史购买应被去重");
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 3000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
                System.Threading.Thread.Sleep(10);
            return condition();
        }
    }

    /// <summary>脚本化核销器：按队列依次返回结果。</summary>
    public sealed class FakeVerifier : IPurchaseVerifier
    {
        private readonly Queue<VerificationResult> _results = new Queue<VerificationResult>();

        public FakeVerifier(params VerificationResult[] results)
        {
            foreach (VerificationResult r in results) _results.Enqueue(r);
        }

        public Task<VerificationResult> VerifyAsync(PurchasePayload payload)
            => Task.FromResult(_results.Count > 0 ? _results.Dequeue() : VerificationResult.Verified);
    }
}

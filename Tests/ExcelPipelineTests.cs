using System;
using System.IO;
using CoffeeBean.Purchase;
using CoffeeBean.Purchase.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CoffeeBean.Purchase.Tests
{
    /// <summary>
    /// Excel 配置管线测试：验证 解析 → 校验 → 生成 .asset/.json 全流程。
    /// 示例 Excel 由 Editor 程序集的 ExcelTestFactory 生成（测试程序集不直接依赖 NPOI）。
    /// </summary>
    public class ExcelPipelineTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "cb_purchase_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        [Test]
        public void Import_ValidAndInvalidRows_KeepsValidAndReportsAllErrors()
        {
            var result = ExcelImporter.Import(ExcelTestFactory.CreateSampleExcel(_tempDir));

            // 3 行合法：gem_100（消耗）、no_ads（ConsumeType=1 → 可消耗）、sub_ok（ConsumeType=2 → 不可消耗）
            Assert.AreEqual(3, result.Products.Count);
            Assert.AreEqual("gem_100", result.Products[0].internalId);
            Assert.AreEqual("no_ads", result.Products[1].internalId);
            Assert.AreEqual("sub_ok", result.Products[2].internalId);
            Assert.AreEqual(IapConsumeType.Consumable, result.Products[0].consumeType);
            Assert.AreEqual(IapConsumeType.Consumable, result.Products[1].consumeType, "ConsumeType=1 应为可消耗（可重复购买）");
            Assert.AreEqual(IapConsumeType.NonConsumable, result.Products[2].consumeType, "ConsumeType=2 应为不可消耗（礼包/永久增益）");

            // 各类问题都被报告
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Exists(e => e.Column == "Id_s"), "空内部 ID 行应被警告跳过");
            Assert.IsTrue(result.Errors.Exists(e => e.Message.Contains("必须是 0/1/2")), "非法类型（3）应报错");
            Assert.IsTrue(result.Errors.Exists(e => e.Message.Contains("重复")), "重复 Google ID 应报错");

            // 警告不阻塞：空 ID 是警告级，非法类型/重复是阻塞级
            var idError = result.Errors.Find(e => e.Column == "Id_s");
            Assert.IsNotNull(idError);
            Assert.IsTrue(idError.IsWarning, "空内部 ID 应为警告级");
            Assert.IsTrue(result.HasBlockingErrors, "非法类型/重复应仍是阻塞错误");
        }

        [Test]
        public void Import_ExplicitIapType_OverridesConsumeTypeMapping()
        {
            var result = ExcelImporter.Import(ExcelTestFactory.CreateExplicitTypeExcel(_tempDir));
            Assert.IsFalse(result.HasBlockingErrors, "显式类型表不应有阻塞错误");

            Assert.AreEqual(IapConsumeType.Consumable, result.Products.Find(p => p.internalId == "a").consumeType, "ConsumeType=1 + IapType=0 → 消耗型");
            Assert.AreEqual(IapConsumeType.Subscription, result.Products.Find(p => p.internalId == "b").consumeType, "ConsumeType=0 + IapType=2 → 订阅");
            Assert.AreEqual(IapConsumeType.NonConsumable, result.Products.Find(p => p.internalId == "c").consumeType, "无 IapType + ConsumeType=2 → 不可消耗");
        }

        [Test]
        public void Import_MissingRequiredColumns_ReportsColumnErrors()
        {
            var result = ExcelImporter.Import(ExcelTestFactory.CreateBadHeaderExcel(_tempDir));
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Errors.Exists(e => e.Column == "GoogleProductId_s"));
            Assert.IsTrue(result.Errors.Exists(e => e.Column == "AppleProductId_s"));
            Assert.IsTrue(result.Errors.Exists(e => e.Column == "ConsumeType_i"));
        }

        [Test]
        public void Import_NonexistentFile_ReportsError()
        {
            var result = ExcelImporter.Import(Path.Combine(_tempDir, "nope.xlsx"));
            Assert.IsTrue(result.HasErrors);
        }

        [Test]
        public void Generate_CreatesAssetAndJson_PreservesGlobalSettings()
        {
            string folder = "Assets/Temp_IapTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.DeleteAsset(folder);
            try
            {
                IapConfig config = ExcelConfigGenerator.Generate(ExcelTestFactory.CreateValidExcel(_tempDir), folder, out string message);
                Assert.IsNotNull(config);
                Assert.AreEqual(2, config.products.Count);

                string assetPath = folder + "/IapConfig.asset";
                string jsonPath = folder + "/IapConfig.json";
                Assert.IsTrue(File.Exists(jsonPath), "应生成 JSON 旁证");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<IapConfig>(assetPath), "应生成 .asset");

                // 修改全局设置后重新生成，应保留
                config.serverVerifyEnabled = true;
                config.verifyTimeoutSeconds = 5f;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();

                IapConfig config2 = ExcelConfigGenerator.Generate(ExcelTestFactory.CreateValidExcel(_tempDir), folder, out _);
                Assert.IsTrue(config2.serverVerifyEnabled, "重新生成应保留服务器核销开关");
                Assert.AreEqual(5f, config2.verifyTimeoutSeconds, "重新生成应保留超时设置");
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Config_FindByPlatformId_MatchesEitherStore()
        {
            var config = ScriptableObject.CreateInstance<IapConfig>();
            config.products.Add(new IapProductDefinition
            {
                internalId = "gem_100",
                googleProductId = "com.example.gem100",
                appleProductId = "com.example.gem100.ios",
                consumeType = IapConsumeType.Consumable
            });

            Assert.AreEqual("gem_100", config.FindByPlatformId("com.example.gem100").internalId);
            Assert.AreEqual("gem_100", config.FindByPlatformId("com.example.gem100.ios").internalId);
            Assert.AreEqual("gem_100", config.FindByInternalId("gem_100").internalId);
            Assert.IsNull(config.FindByInternalId("nope"));
        }
    }
}

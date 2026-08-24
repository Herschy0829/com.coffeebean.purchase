using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>Excel 解析/校验失败时抛出，Message 为可直接展示的错误汇总。</summary>
    public sealed class ExcelImportException : Exception
    {
        public ExcelImporter.ImportResult Result { get; }

        public ExcelImportException(ExcelImporter.ImportResult result)
            : base(FormatErrors(result))
        {
            Result = result;
        }

        private static string FormatErrors(ExcelImporter.ImportResult result)
        {
            if (result == null || result.Errors.Count == 0) return "Excel 解析失败（未知错误）";
            return "Excel 解析失败，共 " + result.Errors.Count + " 个错误:\n" +
                   string.Join("\n", result.Errors.Select(e => e.ToString()));
        }
    }

    /// <summary>
    /// 从 Excel 生成配置产物：IapConfig.asset（运行时数据源）+ IapConfig.json（旁证/CI）。
    /// 重新生成时保留已有资产里的全局设置（服务器核销开关、超时、重试等）。
    /// </summary>
    public static class ExcelConfigGenerator
    {
        public const string DefaultOutputFolder = "Assets/Resources/CoffeeBean";

        public static IapConfig Generate(string excelPath, string outputFolder, out string message)
        {
            if (string.IsNullOrEmpty(outputFolder)) outputFolder = DefaultOutputFolder;

            ExcelImporter.ImportResult result = ExcelImporter.Import(excelPath);
            if (result.HasErrors) throw new ExcelImportException(result);

            EnsureFolder(outputFolder);
            string assetPath = Path.Combine(outputFolder, "IapConfig.asset").Replace('\\', '/');
            string jsonPath = Path.Combine(outputFolder, "IapConfig.json").Replace('\\', '/');

            // 已有资产则保留全局设置
            IapConfig config = AssetDatabase.LoadAssetAtPath<IapConfig>(assetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<IapConfig>();
                AssetDatabase.CreateAsset(config, assetPath);
            }
            bool hadServerVerify = config.serverVerifyEnabled;

            config.products = result.Products
                .OrderBy(p => p.sortOrder)
                .ToList();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            File.WriteAllText(jsonPath, config.ToJson());
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);

            message = $"生成成功：{result.Products.Count} 个商品\n.asset → {assetPath}\n.json → {jsonPath}";
            return config;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}

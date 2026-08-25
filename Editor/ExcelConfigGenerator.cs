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
            if (result == null) return "Excel 解析失败（未知错误）";
            var blocking = result.Errors.Where(e => !e.IsWarning).ToList();
            if (blocking.Count == 0 && result.Products.Count == 0)
                return "没有可生成的有效商品（所有行都被跳过/为空）。";
            string body = blocking.Count > 0
                ? string.Join("\n", blocking.Select(e => e.ToString()))
                : "没有可生成的有效商品。";
            string warn = result.WarningCount > 0 ? $"\n\n（另有 {result.WarningCount} 条警告，相关行已跳过）" : "";
            return $"Excel 解析失败，共 {blocking.Count} 个错误:\n{body}{warn}";
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
            // 只被"阻塞性错误"拦截；警告（商店 ID 无效跳过、注释行等）不阻塞生成
            if (result.HasBlockingErrors) throw new ExcelImportException(result);
            if (result.Products.Count == 0)
                throw new ExcelImportException(result); // 无有效商品，也视为失败

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

            string warningNote = result.WarningCount > 0 ? $"\n（警告 {result.WarningCount} 条，相关行已跳过）" : "";
            message = $"生成成功：{result.Products.Count} 个商品{warningNote}\n.asset → {assetPath}\n.json → {jsonPath}";
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

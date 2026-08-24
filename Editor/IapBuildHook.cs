using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// 打包监听：打包前自动重新解析 Excel 生成最新配置（可在配置资产里关闭）。
    /// 解析/校验失败时中止打包并弹窗提示。
    /// </summary>
    public sealed class IapBuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string excelPath = EditorPrefs.GetString(IapConfigWindow.ExcelPathPrefKey, string.Empty);
            if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
            {
                Debug.LogWarning("[CoffeeBean.Purchase] 未配置 Excel 商品表（或文件不存在），跳过打包前重解析。");
                return;
            }

            string outputFolder = EditorPrefs.GetString(IapConfigWindow.OutputFolderPrefKey, ExcelConfigGenerator.DefaultOutputFolder);
            IapConfig config = LoadConfig(outputFolder);
            if (config != null && !config.forceReparseOnBuild)
            {
                Debug.Log("[CoffeeBean.Purchase] 打包前重解析已关闭（IapConfig.forceReparseOnBuild=false），使用现有配置。");
                return;
            }

            try
            {
                ExcelConfigGenerator.Generate(excelPath, outputFolder, out string message);
                Debug.Log("[CoffeeBean.Purchase] 打包前配置已重新生成：\n" + message);
            }
            catch (ExcelImportException e)
            {
                string text = "IAP 配置重新生成失败，打包已中止。\n\n" + e.Message;
                Debug.LogError("[CoffeeBean.Purchase] " + text);
                EditorUtility.DisplayDialog("CoffeeBean Purchase - 打包中止", text, "OK");
                throw new BuildFailedException(text);
            }
        }

        private static IapConfig LoadConfig(string outputFolder)
        {
            string path = Path.Combine(outputFolder, "IapConfig.asset").Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<IapConfig>(path);
        }
    }
}

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// 支付模块配置窗口：Window > CoffeeBean > Purchase Config。
    /// 选择 Excel 表 → 校验弹窗 → 生成 IapConfig(.asset + .json)；可随时手动重新生成。
    /// </summary>
    public sealed class IapConfigWindow : EditorWindow
    {
        public const string ExcelPathPrefKey = "CoffeeBean.Purchase.ExcelPath";
        public const string OutputFolderPrefKey = "CoffeeBean.Purchase.OutputFolder";

        private string _excelPath;
        private string _outputFolder;
        private IapConfig _config;
        private Vector2 _scroll;

        [MenuItem("Window/CoffeeBean/Purchase Config")]
        public static void Open()
        {
            var window = GetWindow<IapConfigWindow>("CoffeeBean Purchase Config");
            window.Reload();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            _excelPath = EditorPrefs.GetString(ExcelPathPrefKey, string.Empty);
            _outputFolder = EditorPrefs.GetString(OutputFolderPrefKey, ExcelConfigGenerator.DefaultOutputFolder);
            _config = LoadConfigAsset();
        }

        private IapConfig LoadConfigAsset()
        {
            string path = Path.Combine(_outputFolder, "IapConfig.asset").Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<IapConfig>(path);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Excel 商品表配置", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // 选择 Excel
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Excel 表", GUILayout.Width(70));
            _excelPath = EditorGUILayout.TextField(_excelPath);
            if (GUILayout.Button("选择...", GUILayout.Width(70)))
            {
                string picked = EditorUtility.OpenFilePanel("选择商品配置 Excel", "", "xlsx,xls");
                if (!string.IsNullOrEmpty(picked))
                {
                    _excelPath = picked;
                    EditorPrefs.SetString(ExcelPathPrefKey, _excelPath);
                    ValidateAndShow(_excelPath, showSuccess: true);
                }
            }
            EditorGUILayout.EndHorizontal();

            // 输出目录
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("输出目录", GUILayout.Width(70));
            string newFolder = EditorGUILayout.TextField(_outputFolder);
            if (newFolder != _outputFolder)
            {
                _outputFolder = newFolder;
                EditorPrefs.SetString(OutputFolderPrefKey, _outputFolder);
                _config = LoadConfigAsset();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新生成配置", GUILayout.Height(30)))
                Regenerate();
            if (GUILayout.Button("校验 Excel（不生成）", GUILayout.Height(30)))
                ValidateAndShow(_excelPath, showSuccess: true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("全局设置（保存到生成的配置资产）", EditorStyles.boldLabel);
            DrawGlobalSettings();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("已生成商品（" + ProductCount() + " 个）", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_config != null)
            {
                foreach (var p in _config.products)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(p.enabled ? "●" : "○", GUILayout.Width(16));
                    EditorGUILayout.LabelField(p.internalId, GUILayout.Width(140));
                    EditorGUILayout.LabelField(p.consumeType.ToString(), GUILayout.Width(110));
                    EditorGUILayout.LabelField("G:" + p.googleProductId, GUILayout.MinWidth(120));
                    EditorGUILayout.LabelField("A:" + p.appleProductId, GUILayout.MinWidth(120));
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("尚未生成配置。选择 Excel 后点击“重新生成配置”。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private int ProductCount() => _config != null ? _config.products.Count : 0;

        private void DrawGlobalSettings()
        {
            if (_config == null) return;

            EditorGUI.BeginChangeCheck();
            _config.serverVerifyEnabled = EditorGUILayout.Toggle("服务器二次确认", _config.serverVerifyEnabled);
            _config.verifyTimeoutSeconds = EditorGUILayout.FloatField("验证超时（秒）", _config.verifyTimeoutSeconds);
            _config.verifyRetryCount = EditorGUILayout.IntField("验证重试次数", _config.verifyRetryCount);
            _config.forceReparseOnBuild = EditorGUILayout.Toggle("打包前强制重解析", _config.forceReparseOnBuild);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
            }
        }

        private void Regenerate()
        {
            if (string.IsNullOrEmpty(_excelPath))
            {
                EditorUtility.DisplayDialog("CoffeeBean Purchase", "请先选择 Excel 表。", "OK");
                return;
            }
            try
            {
                _config = ExcelConfigGenerator.Generate(_excelPath, _outputFolder, out string message);
                EditorPrefs.SetString(ExcelPathPrefKey, _excelPath);
                EditorUtility.DisplayDialog("CoffeeBean Purchase", message, "OK");
            }
            catch (ExcelImportException e)
            {
                EditorUtility.DisplayDialog("CoffeeBean Purchase - 解析失败", e.Message, "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("CoffeeBean Purchase - 生成失败", e.Message, "OK");
            }
        }

        private void ValidateAndShow(string excelPath, bool showSuccess)
        {
            if (string.IsNullOrEmpty(excelPath)) return;
            var result = ExcelImporter.Import(excelPath);
            if (result.HasErrors)
            {
                EditorUtility.DisplayDialog("CoffeeBean Purchase - 校验失败",
                    "共 " + result.Errors.Count + " 个错误:\n" +
                    string.Join("\n", System.Linq.Enumerable.Take(result.Errors.Select(e => e.ToString()), 30)),
                    "OK");
            }
            else if (showSuccess)
            {
                EditorUtility.DisplayDialog("CoffeeBean Purchase", $"校验通过：{result.Products.Count} 个商品。", "OK");
            }
        }
    }
}

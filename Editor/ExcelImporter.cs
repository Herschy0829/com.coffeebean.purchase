using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// Excel 商品表解析与校验。
    /// v0.1.6 起读取层（MiniExcel / 表头检测 / 列名归一 / 单元格读取）迁移到 excel 模块
    /// （<see cref="CExcelReader"/>），本类只保留 IAP 特有逻辑：
    /// ConsumeType/IapType 映射、价格宽松解析、商店 ID 校验、货币/JSON 校验、重复检测。
    /// 列名规范：字段名 + 类型后缀（Id_s / GoogleProductId_s / ConsumeType_i / ...），详见 design-iap.md §3。
    /// </summary>
    public static class ExcelImporter
    {
        public sealed class ImportError
        {
            public int Row;
            public string Column;
            public string Message;

            /// <summary>警告级：不阻塞生成（如商店 ID 无效跳过、空行、注释行）。</summary>
            public bool IsWarning;

            public override string ToString() => $"第 {Row} 行 [{Column}]: {Message}";
        }

        public sealed class ImportResult
        {
            public string SourcePath;
            public List<IapProductDefinition> Products = new List<IapProductDefinition>();
            public List<ImportError> Errors = new List<ImportError>();
            public bool HasErrors => Errors.Count > 0;

            /// <summary>是否存在阻塞性错误（警告不算）。</summary>
            public bool HasBlockingErrors { get { foreach (var e in Errors) if (!e.IsWarning) return true; return false; } }

            public int WarningCount { get { int n = 0; foreach (var e in Errors) if (e.IsWarning) n++; return n; } }
        }

        private const string ColInternalId = "Id_s";
        private const string ColGoogleId = "GoogleProductId_s";
        private const string ColAppleId = "AppleProductId_s";
        private const string ColConsumeType = "ConsumeType_i";
        private const string ColIapType = "IapType_i";
        private const string ColTitle = "Title_s";
        private const string ColDescription = "Description_s";
        private const string ColPrice = "Price_f";
        private const string ColCurrency = "Currency_s";
        private const string ColEnabled = "Enabled_i";
        private const string ColGroup = "Group_s";
        private const string ColSortOrder = "SortOrder_i";
        private const string ColVerify = "Verify_i";
        private const string ColExtra = "Extra_s";

        /// <summary>
        /// 列名别名映射：逻辑字段 → 可接受的列名列表。
        /// 兼容不同团队的命名习惯（如 ID_i / DefaultPrice_s / 中文表头）。
        /// </summary>
        private static readonly Dictionary<string, string[]> ColumnAliases = new Dictionary<string, string[]>
        {
            [ColInternalId] = new[] { "Id_s", "ID_i", "商品ID", "内部ID" },
            [ColGoogleId] = new[] { "GoogleProductId_s", "Google商品ID" },
            [ColAppleId] = new[] { "AppleProductId_s", "Apple商品ID" },
            [ColConsumeType] = new[] { "ConsumeType_i", "商品类型" },
            [ColIapType] = new[] { "IapType_i", "商店类型", "IAP类型" },
            [ColTitle] = new[] { "Title_s", "显示名称" },
            [ColDescription] = new[] { "Description_s", "商品描述" },
            [ColPrice] = new[] { "Price_f", "DefaultPrice_s", "默认显示价格" },
            [ColCurrency] = new[] { "Currency_s", "货币" },
            [ColEnabled] = new[] { "Enabled_i", "是否启用" },
            [ColGroup] = new[] { "Group_s" },
            [ColSortOrder] = new[] { "SortOrder_i", "排序权重" },
            [ColVerify] = new[] { "Verify_i" },
            [ColExtra] = new[] { "Extra_s" },
        };

        /// <summary>解析 Excel 文件（.xlsx）。路径不存在 / 文件损坏时返回带错误的结果（不抛异常）。</summary>
        public static ImportResult Import(string excelPath)
        {
            var result = new ImportResult { SourcePath = excelPath };

            // 读取层：表头检测 / 列名归一（别名）/ 空行与 # 注释行跳过 / 问题分级 由 excel 模块完成
            CExcelReadResult read = CExcelReader.Read(excelPath, new CExcelReadOptions
            {
                ColumnAliases = ColumnAliases,
            });
            foreach (CExcelIssue issue in read.Issues)
            {
                result.Errors.Add(new ImportError
                {
                    Row = issue.Row,
                    Column = issue.Column,
                    Message = issue.Message,
                    IsWarning = issue.Level == CExcelIssueLevel.Warning,
                });
            }
            if (result.HasBlockingErrors) return result;

            // 校验必填列存在
            var columnSet = new HashSet<string>(read.Columns, StringComparer.OrdinalIgnoreCase);
            foreach (string required in new[] { ColInternalId, ColGoogleId, ColAppleId, ColConsumeType })
            {
                if (!columnSet.Contains(required))
                    result.Errors.Add(new ImportError { Row = 0, Column = required, Message = "缺少必填列: " + required });
            }
            if (result.Errors.Count > 0) return result;

            // 数据行（read.Rows 已跳过空行 / # 注释行；Excel 行号 = 表头行号 + 1 + 行索引）
            var productRows = new List<int>();
            for (int i = 0; i < read.Rows.Count; i++)
            {
                Dictionary<string, object> row = read.Rows[i];
                int rowNumber = read.HeaderRowIndex + i + 2;

                var def = new IapProductDefinition();
                int errorStart = result.Errors.Count;

                def.internalId = Cell(row, ColInternalId);
                def.googleProductId = Cell(row, ColGoogleId);
                def.appleProductId = Cell(row, ColAppleId);
                // 商品类型：优先 IapType_i（显式商店类型 0/1/2），否则按 ConsumeType_i 映射
                string explicitTypeText = Cell(row, ColIapType);
                string consumeTypeText = Cell(row, ColConsumeType);
                bool consumeTypeNumeric = int.TryParse(consumeTypeText, out _);

                // 注释/说明行（如底部图例）：内部 ID 为空 且 类型列是非数字文本 → 警告跳过，不报错
                if (string.IsNullOrEmpty(def.internalId) && !consumeTypeNumeric && string.IsNullOrEmpty(explicitTypeText))
                {
                    result.Errors.Add(new ImportError
                    {
                        Row = rowNumber,
                        Column = ColConsumeType,
                        Message = "类型列为非数字（疑似注释文本），该行视为注释行已跳过",
                        IsWarning = true,
                    });
                }
                else if (!string.IsNullOrEmpty(explicitTypeText))
                {
                    def.consumeType = ParseExplicitType(explicitTypeText, result, rowNumber);
                }
                else
                {
                    def.consumeType = ParseConsumeType(consumeTypeText, result, rowNumber);
                }
                def.title = Cell(row, ColTitle);
                def.description = Cell(row, ColDescription);
                def.priceAnchor = ParsePriceTolerant(Cell(row, ColPrice));
                def.currency = Cell(row, ColCurrency);
                def.enabled = ParseEnabled(Cell(row, ColEnabled), result, rowNumber);
                def.group = Cell(row, ColGroup);
                def.sortOrder = (int)ParseFloat(Cell(row, ColSortOrder), result, rowNumber, ColSortOrder, "排序必须为整数");
                def.serverVerifyOverride = (int)ParseVerify(Cell(row, ColVerify), result, rowNumber);
                def.extra = Cell(row, ColExtra);

                // 内部商品 ID 为空 → 视为注释/空行，警告并跳过（不阻塞）
                if (string.IsNullOrEmpty(def.internalId))
                {
                    result.Errors.Add(new ImportError
                    {
                        Row = rowNumber,
                        Column = ColInternalId,
                        Message = "内部商品 ID 为空，该行视为注释/占位，已跳过",
                        IsWarning = true,
                    });
                }

                // Google/Apple 商店 ID 无效（空 / 占位符）→ 该商品不参与初始化，整行跳过（警告，不阻塞生成）
                if (IsValidStoreId(def.googleProductId) == false || IsValidStoreId(def.appleProductId) == false)
                {
                    result.Errors.Add(new ImportError
                    {
                        Row = rowNumber,
                        Column = ColGoogleId,
                        Message = $"商店 ID 无效（Google:'{def.googleProductId}' Apple:'{def.appleProductId}'），该商品已跳过、不参与初始化",
                        IsWarning = true,
                    });
                }

                // 货币格式
                if (!string.IsNullOrEmpty(def.currency) && !IsCurrencyCode(def.currency))
                    result.Errors.Add(new ImportError { Row = rowNumber, Column = ColCurrency, Message = "货币代码必须是 3 位大写字母（如 USD/CNY），实际: " + def.currency });

                // Extra 应为合法 JSON（警告级）
                if (!string.IsNullOrEmpty(def.extra) && !IsJson(def.extra))
                    result.Errors.Add(new ImportError { Row = rowNumber, Column = ColExtra, Message = "Extra 不是合法 JSON（忽略）", IsWarning = true });

                if (result.Errors.Count == errorStart)
                {
                    result.Products.Add(def);
                    productRows.Add(rowNumber);
                }
            }

            // 重复校验
            CheckDuplicates(result, productRows);

            return result;
        }

        /// <summary>按规范列名取单元格文本（trim 后）。</summary>
        private static string Cell(Dictionary<string, object> row, string column)
            => row.TryGetValue(column, out object v) ? CExcelValue.ToText(v).Trim() : string.Empty;

        // ===== 字段解析（IAP 特有，v0.1.6 保留） =====

        private static IapConsumeType ParseConsumeType(string text, ImportResult result, int row)
        {
            if (!int.TryParse(text, out int v))
            {
                result.Errors.Add(new ImportError { Row = row, Column = ColConsumeType, Message = "商品类型必须是整数 0/1/2，实际: '" + text + "'" });
                return IapConsumeType.Consumable;
            }
            switch (v)
            {
                case 0: return IapConsumeType.Consumable;
                // 项目约定：1 = 可消耗（可重复购买，钻石/资源包/特权卡），2 = 不可消耗（礼包/永久增益）
                case 1: return IapConsumeType.Consumable;
                case 2: return IapConsumeType.NonConsumable;
                default:
                    result.Errors.Add(new ImportError { Row = row, Column = ColConsumeType, Message = "商品类型必须是 0/1/2，实际: " + v });
                    return IapConsumeType.Consumable;
            }
        }

        /// <summary>显式商店类型（IapType_i）：直接映射 Unity IAP 的 ProductType，0/1/2。</summary>
        private static IapConsumeType ParseExplicitType(string text, ImportResult result, int row)
        {
            if (int.TryParse(text, out int v) && v >= 0 && v <= 2)
                return (IapConsumeType)v;
            result.Errors.Add(new ImportError { Row = row, Column = ColIapType, Message = "IapType_i 必须是 0（消耗）/1（非消耗）/2（订阅），实际: '" + text + "'" });
            return IapConsumeType.Consumable;
        }

        private static float ParseFloat(string text, ImportResult result, int row, string column, string error)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            if (!float.TryParse(text, out float v) || v < 0)
            {
                result.Errors.Add(new ImportError { Row = row, Column = column, Message = error + "，实际: '" + text + "'" });
                return 0f;
            }
            return v;
        }

        private static bool ParseEnabled(string text, ImportResult result, int row)
        {
            if (string.IsNullOrEmpty(text)) return true;
            if (text == "1") return true;
            if (text == "0") return false;
            result.Errors.Add(new ImportError { Row = row, Column = ColEnabled, Message = "Enabled_i 必须是 0 或 1，实际: '" + text + "'" });
            return true;
        }

        /// <summary>宽松价格解析：取字符串开头的数字部分（"30"→30，"¥68"→68，"$1.99"→1.99），解析失败返回 0。</summary>
        private static float ParsePriceTolerant(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0f;
            var m = Regex.Match(text.Trim(), @"\d+(\.\d+)?");
            return m.Success ? float.Parse(m.Value) : 0f;
        }

        /// <summary>商店 ID 是否有效：空 / 空白 / 占位符（-、0、TBD、待定 等）视为无效，无效则商品不参与初始化。</summary>
        private static readonly string[] StoreIdPlaceholders =
        {
            "-", "--", "0", "none", "null", "n/a", "na", "tbd", "todo", "待定", "无", "未定", "暂无",
        };

        private static bool IsValidStoreId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            string trimmed = id.Trim();
            foreach (string placeholder in StoreIdPlaceholders)
            {
                if (string.Equals(trimmed, placeholder, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private static float ParseVerify(string text, ImportResult result, int row)
        {
            if (string.IsNullOrEmpty(text)) return -1f;
            if (text == "-1" || text == "0" || text == "1") return float.Parse(text);
            result.Errors.Add(new ImportError { Row = row, Column = ColVerify, Message = "Verify_i 必须是 -1（跟随全局）/0（否）/1（是），实际: '" + text + "'" });
            return -1f;
        }

        private static void CheckDuplicates(ImportResult result, List<int> productRows)
        {
            var seenInternal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenGoogle = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var seenApple = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var removeAt = new List<int>();

            for (int i = 0; i < result.Products.Count; i++)
            {
                IapProductDefinition p = result.Products[i];
                int row = productRows[i];
                bool dup = false;
                dup |= CheckOne(p.internalId, row, ColInternalId, seenInternal, result, "内部商品 ID 重复");
                dup |= CheckOne(p.googleProductId, row, ColGoogleId, seenGoogle, result, "Google 商品 ID 重复");
                dup |= CheckOne(p.appleProductId, row, ColAppleId, seenApple, result, "Apple 商品 ID 重复");
                if (dup) removeAt.Add(i);
            }

            for (int i = removeAt.Count - 1; i >= 0; i--)
            {
                result.Products.RemoveAt(removeAt[i]);
                productRows.RemoveAt(removeAt[i]);
            }
        }

        private static bool CheckOne(string value, int row, string column, Dictionary<string, int> seen,
            ImportResult result, string message)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (seen.TryGetValue(value, out int firstRow))
            {
                result.Errors.Add(new ImportError { Row = row, Column = column, Message = message + "（首次出现于第 " + firstRow + " 行）: " + value });
                return true;
            }
            seen[value] = row;
            return false;
        }

        private static bool IsCurrencyCode(string s) => s.Length == 3 && IsAllUpperAlpha(s);

        private static bool IsAllUpperAlpha(string s)
        {
            foreach (char c in s)
                if (c < 'A' || c > 'Z') return false;
            return true;
        }

        [Serializable]
        private class JsonProbe { }

        private static bool IsJson(string s)
        {
            try { UnityEngine.JsonUtility.FromJson<JsonProbe>(s); return true; }
            catch { return false; }
        }
    }
}

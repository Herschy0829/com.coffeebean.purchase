using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CoffeeBean.Purchase;
using MiniExcelLibs;
using UnityEngine;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// Excel 商品表解析与校验（MiniExcel，轻量只读）。
    /// 列名规范：字段名 + 类型后缀（Id_s / GoogleProductId_s / AppleProductId_s / ConsumeType_i / ...），
    /// 详见设计文档 design-iap.md §3。解析失败 / 校验失败均以错误列表形式返回，由调用方弹窗。
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
            if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
            {
                result.Errors.Add(new ImportError { Row = 0, Column = "-", Message = "Excel 文件不存在: " + excelPath });
                return result;
            }

            try
            {
                // useHeaderRow:false → 每行是 IDictionary<string,object>，键为列字母（A/B/C...）
                var rows = new List<IDictionary<string, object>>();
                foreach (dynamic row in MiniExcel.Query(excelPath, useHeaderRow: false))
                    rows.Add((IDictionary<string, object>)row);

                if (rows.Count == 0)
                {
                    result.Errors.Add(new ImportError { Row = 0, Column = "-", Message = "Excel 工作表为空" });
                    return result;
                }

                // 表头检测：前 3 行中匹配已知列名最多的行（支持"中文说明行 + 字段名行"的双行表头）
                int headerIndex = FindHeaderRow(rows);
                var headerRow = rows[headerIndex];
                // 逻辑字段名 → 列字母（MiniExcel useHeaderRow:false 时键为列字母）
                var columnIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int maxCol = headerRow.Count;
                for (int c = 0; c < maxCol; c++)
                {
                    string name = ToText(GetCell(headerRow, c)).Trim();
                    if (string.IsNullOrEmpty(name)) continue;
                    foreach (KeyValuePair<string, string[]> kv in ColumnAliases)
                    {
                        foreach (string alias in kv.Value)
                        {
                            if (string.Equals(name, alias, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!columnIndex.ContainsKey(kv.Key))
                                    columnIndex[kv.Key] = ColumnLetter(c);
                                break;
                            }
                        }
                    }
                }

                // 校验必填列存在
                foreach (string required in new[] { ColInternalId, ColGoogleId, ColAppleId, ColConsumeType })
                {
                    if (!columnIndex.ContainsKey(required))
                        result.Errors.Add(new ImportError { Row = 0, Column = required, Message = "缺少必填列: " + required });
                }
                if (result.Errors.Count > 0) return result;

                // 数据行（表头之后，Excel 行号 = 行索引 + 1）
                var productRows = new List<int>();
                for (int r = headerIndex + 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    int rowNumber = r + 1;
                    if (IsRowEmpty(row)) continue;

                    var def = new IapProductDefinition();
                    int errorStart = result.Errors.Count;

                    def.internalId = ToText(GetCell(row, columnIndex, ColInternalId)).Trim();
                    def.googleProductId = ToText(GetCell(row, columnIndex, ColGoogleId)).Trim();
                    def.appleProductId = ToText(GetCell(row, columnIndex, ColAppleId)).Trim();
                    // 商品类型：优先 IapType_i（显式商店类型 0/1/2），否则按 ConsumeType_i 映射
                    string explicitTypeText = ToText(GetCell(row, columnIndex, ColIapType)).Trim();
                    string consumeTypeText = ToText(GetCell(row, columnIndex, ColConsumeType)).Trim();
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
                    def.title = ToText(GetCell(row, columnIndex, ColTitle)).Trim();
                    def.description = ToText(GetCell(row, columnIndex, ColDescription)).Trim();
                    def.priceAnchor = ParsePriceTolerant(ToText(GetCell(row, columnIndex, ColPrice)));
                    def.currency = ToText(GetCell(row, columnIndex, ColCurrency)).Trim();
                    def.enabled = ParseEnabled(ToText(GetCell(row, columnIndex, ColEnabled)), result, rowNumber);
                    def.group = ToText(GetCell(row, columnIndex, ColGroup)).Trim();
                    def.sortOrder = (int)ParseFloat(ToText(GetCell(row, columnIndex, ColSortOrder)), result, rowNumber, ColSortOrder, "排序必须为整数");
                    def.serverVerifyOverride = (int)ParseVerify(ToText(GetCell(row, columnIndex, ColVerify)), result, rowNumber);
                    def.extra = ToText(GetCell(row, columnIndex, ColExtra)).Trim();

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
            catch (Exception e)
            {
                result.Errors.Add(new ImportError { Row = 0, Column = "-", Message = "Excel 解析失败: " + e.Message });
                return result;
            }
        }

        // ===== 表头检测 / 单元格读取 =====

        /// <summary>在前 3 行中找到匹配已知列名最多的行作为表头（兼容双行表头）。找不到则用第 1 行。</summary>
        private static int FindHeaderRow(List<IDictionary<string, object>> rows)
        {
            int best = 0;
            int bestCount = -1;
            int scan = Math.Min(rows.Count, 3);
            for (int i = 0; i < scan; i++)
            {
                int count = CountHeaderMatches(rows[i]);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = i;
                }
            }
            return best;
        }

        private static int CountHeaderMatches(IDictionary<string, object> row)
        {
            // 只统计"规范列名"（每组别名中的第一个，如 GoogleProductId_s）——
            // 字段名行优先于中文说明行，避免双行表头选错
            int count = 0;
            for (int c = 0; c < row.Count; c++)
            {
                string name = ToText(GetCell(row, c)).Trim();
                if (string.IsNullOrEmpty(name)) continue;
                foreach (KeyValuePair<string, string[]> kv in ColumnAliases)
                {
                    if (string.Equals(name, kv.Value[0], StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        private static object GetCell(IDictionary<string, object> row, Dictionary<string, string> columnIndex, string column)
        {
            if (!columnIndex.TryGetValue(column, out string letter)) return null;
            return row.TryGetValue(letter, out object v) ? v : null;
        }

        private static object GetCell(IDictionary<string, object> row, int index)
        {
            // MiniExcel useHeaderRow:false 时键为列字母
            string key = ColumnLetter(index);
            return row.TryGetValue(key, out object v) ? v : null;
        }

        private static string ColumnLetter(int index)
        {
            string s = string.Empty;
            int n = index;
            while (n >= 0)
            {
                s = (char)('A' + (n % 26)) + s;
                n = n / 26 - 1;
            }
            return s;
        }

        private static string ToText(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "1" : "0";
                case double d: return IsIntegral(d) ? ((long)d).ToString() : d.ToString("R");
                case float f: return IsIntegral(f) ? ((long)f).ToString() : f.ToString("R");
                case decimal m: return IsIntegral(m) ? ((long)m).ToString() : m.ToString();
                case int i: return i.ToString();
                case long l: return l.ToString();
                case DateTime dt: return dt.ToString("yyyy-MM-dd HH:mm:ss");
                default: return value.ToString() ?? string.Empty;
            }
        }

        private static bool IsIntegral(double d) => d == Math.Floor(d) && Math.Abs(d) < 9.2e18;

        private static bool IsIntegral(float f) => f == Math.Floor(f) && Math.Abs(f) < 9.2e18;

        private static bool IsIntegral(decimal m) => m == Math.Floor(m);

        private static bool IsRowEmpty(IDictionary<string, object> row)
        {
            foreach (var kv in row)
            {
                if (kv.Value != null && ToText(kv.Value).Length > 0) return false;
            }
            return true;
        }

        // ===== 字段解析（与 NPOI 版一致）=====

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
                case 1: return IapConsumeType.NonConsumable;
                // 按项目约定：2 = 礼包/特权类，映射为非消耗型（显式订阅请用 IapType_i=2）
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
            try { JsonUtility.FromJson<JsonProbe>(s); return true; }
            catch { return false; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
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

            public override string ToString() => $"第 {Row} 行 [{Column}]: {Message}";
        }

        public sealed class ImportResult
        {
            public string SourcePath;
            public List<IapProductDefinition> Products = new List<IapProductDefinition>();
            public List<ImportError> Errors = new List<ImportError>();
            public bool HasErrors => Errors.Count > 0;
        }

        private const string ColInternalId = "Id_s";
        private const string ColGoogleId = "GoogleProductId_s";
        private const string ColAppleId = "AppleProductId_s";
        private const string ColConsumeType = "ConsumeType_i";
        private const string ColTitle = "Title_s";
        private const string ColDescription = "Description_s";
        private const string ColPrice = "Price_f";
        private const string ColCurrency = "Currency_s";
        private const string ColEnabled = "Enabled_i";
        private const string ColGroup = "Group_s";
        private const string ColSortOrder = "SortOrder_i";
        private const string ColVerify = "Verify_i";
        private const string ColExtra = "Extra_s";

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

                // 表头 = 第一行（列字母 → 列名映射，列名去空格、大小写不敏感）
                var headerRow = rows[0];
                var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int maxCol = headerRow.Count;
                for (int c = 0; c < maxCol; c++)
                {
                    string name = ToText(GetCell(headerRow, c)).Trim();
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!columnIndex.ContainsKey(name)) columnIndex[name] = c;
                }

                // 校验必填列存在
                foreach (string required in new[] { ColInternalId, ColGoogleId, ColAppleId, ColConsumeType })
                {
                    if (!columnIndex.ContainsKey(required))
                        result.Errors.Add(new ImportError { Row = 0, Column = required, Message = "缺少必填列: " + required });
                }
                if (result.Errors.Count > 0) return result;

                // 数据行（第 2 行起，Excel 行号 = 行索引 + 1）
                var productRows = new List<int>();
                for (int r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    int rowNumber = r + 1;
                    if (IsRowEmpty(row)) continue;

                    var def = new IapProductDefinition();
                    int errorStart = result.Errors.Count;

                    def.internalId = ToText(GetCell(row, columnIndex[ColInternalId])).Trim();
                    def.googleProductId = ToText(GetCell(row, columnIndex[ColGoogleId])).Trim();
                    def.appleProductId = ToText(GetCell(row, columnIndex[ColAppleId])).Trim();
                    def.consumeType = ParseConsumeType(ToText(GetCell(row, columnIndex[ColConsumeType])), result, rowNumber);
                    def.title = ToText(GetCell(row, columnIndex, ColTitle)).Trim();
                    def.description = ToText(GetCell(row, columnIndex, ColDescription)).Trim();
                    def.priceAnchor = ParseFloat(ToText(GetCell(row, columnIndex, ColPrice)), result, rowNumber, ColPrice, "价格锚点必须为 >= 0 的数字");
                    def.currency = ToText(GetCell(row, columnIndex, ColCurrency)).Trim();
                    def.enabled = ParseEnabled(ToText(GetCell(row, columnIndex, ColEnabled)), result, rowNumber);
                    def.group = ToText(GetCell(row, columnIndex, ColGroup)).Trim();
                    def.sortOrder = (int)ParseFloat(ToText(GetCell(row, columnIndex, ColSortOrder)), result, rowNumber, ColSortOrder, "排序必须为整数");
                    def.serverVerifyOverride = (int)ParseVerify(ToText(GetCell(row, columnIndex, ColVerify)), result, rowNumber);
                    def.extra = ToText(GetCell(row, columnIndex, ColExtra)).Trim();

                    // 必填
                    if (string.IsNullOrEmpty(def.internalId))
                        result.Errors.Add(new ImportError { Row = rowNumber, Column = ColInternalId, Message = "内部商品 ID 不能为空" });
                    if (string.IsNullOrEmpty(def.googleProductId))
                        result.Errors.Add(new ImportError { Row = rowNumber, Column = ColGoogleId, Message = "Google 商品 ID 不能为空" });
                    if (string.IsNullOrEmpty(def.appleProductId))
                        result.Errors.Add(new ImportError { Row = rowNumber, Column = ColAppleId, Message = "Apple 商品 ID 不能为空" });

                    // 货币格式
                    if (!string.IsNullOrEmpty(def.currency) && !IsCurrencyCode(def.currency))
                        result.Errors.Add(new ImportError { Row = rowNumber, Column = ColCurrency, Message = "货币代码必须是 3 位大写字母（如 USD/CNY），实际: " + def.currency });

                    // Extra 应为合法 JSON（警告级）
                    if (!string.IsNullOrEmpty(def.extra) && !IsJson(def.extra))
                        result.Errors.Add(new ImportError { Row = rowNumber, Column = ColExtra, Message = "Extra 不是合法 JSON（忽略）" });

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

        // ===== 单元格读取 =====

        private static object GetCell(IDictionary<string, object> row, Dictionary<string, int> columnIndex, string column)
        {
            if (!columnIndex.TryGetValue(column, out int index)) return null;
            return GetCell(row, index);
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
                result.Errors.Add(new ImportError { Row = row, Column = ColConsumeType, Message = "商品类型必须是整数 0/1，实际: '" + text + "'" });
                return IapConsumeType.Consumable;
            }
            if (v == (int)IapConsumeType.Subscription)
            {
                result.Errors.Add(new ImportError { Row = row, Column = ColConsumeType, Message = "订阅（ConsumeType=2）v1 暂不支持，请使用 0（消耗型）或 1（非消耗型）" });
                return IapConsumeType.Subscription;
            }
            if (v != 0 && v != 1)
            {
                result.Errors.Add(new ImportError { Row = row, Column = ColConsumeType, Message = "商品类型必须是 0 或 1，实际: " + v });
                return IapConsumeType.Consumable;
            }
            return (IapConsumeType)v;
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

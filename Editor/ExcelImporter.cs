using System;
using System.Collections.Generic;
using System.IO;
using CoffeeBean.Purchase;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEngine;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// Excel 商品表解析与校验（NPOI）。
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

        /// <summary>解析 Excel 文件。路径不存在 / 文件损坏时返回带错误的结果（不抛异常）。</summary>
        public static ImportResult Import(string excelPath)
        {
            var result = new ImportResult { SourcePath = excelPath };
            if (string.IsNullOrEmpty(excelPath) || !File.Exists(excelPath))
            {
                result.Errors.Add(new ImportError { Row = 0, Column = "-", Message = "Excel 文件不存在: " + excelPath });
                return result;
            }

            IWorkbook workbook = null;
            try
            {
                using (var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    workbook = excelPath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                        ? (IWorkbook)new HSSFWorkbook(fs)
                        : (IWorkbook)new XSSFWorkbook(fs);
                }

                ISheet sheet = workbook.GetSheetAt(0);
                if (sheet == null)
                {
                    result.Errors.Add(new ImportError { Row = 0, Column = "-", Message = "工作簿中没有工作表" });
                    return result;
                }

                // 表头
                IRow headerRow = sheet.GetRow(sheet.FirstRowNum);
                if (headerRow == null)
                {
                    result.Errors.Add(new ImportError { Row = 0, Column = "-", Message = "第 1 行必须是表头（列名）" });
                    return result;
                }

                var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = headerRow.FirstCellNum; c < headerRow.LastCellNum; c++)
                {
                    string name = GetCellText(headerRow.GetCell(c));
                    if (string.IsNullOrEmpty(name)) continue;
                    name = name.Trim();
                    if (!columnIndex.ContainsKey(name)) columnIndex[name] = c;
                }

                // 校验必填列存在
                foreach (string required in new[] { ColInternalId, ColGoogleId, ColAppleId, ColConsumeType })
                {
                    if (!columnIndex.ContainsKey(required))
                        result.Errors.Add(new ImportError { Row = 0, Column = required, Message = "缺少必填列: " + required });
                }
                if (result.Errors.Count > 0) return result;

                // 数据行
                var productRows = new List<int>();
                for (int r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
                {
                    IRow row = sheet.GetRow(r);
                    if (row == null) continue;
                    // 整行为空则跳过
                    if (IsRowEmpty(row)) continue;

                    var def = new IapProductDefinition();
                    int errorStart = result.Errors.Count;
                    int rowNumber = r + 1;

                    def.internalId = GetCellText(GetCell(row, columnIndex, ColInternalId)).Trim();
                    def.googleProductId = GetCellText(GetCell(row, columnIndex, ColGoogleId)).Trim();
                    def.appleProductId = GetCellText(GetCell(row, columnIndex, ColAppleId)).Trim();
                    def.consumeType = ParseConsumeType(GetCellText(GetCell(row, columnIndex, ColConsumeType)), result, rowNumber);
                    def.title = GetCellText(GetCell(row, columnIndex, ColTitle)).Trim();
                    def.description = GetCellText(GetCell(row, columnIndex, ColDescription)).Trim();
                    def.priceAnchor = ParseFloat(GetCellText(GetCell(row, columnIndex, ColPrice)), result, rowNumber, ColPrice, "价格锚点必须为 >= 0 的数字");
                    def.currency = GetCellText(GetCell(row, columnIndex, ColCurrency)).Trim();
                    def.enabled = ParseEnabled(GetCellText(GetCell(row, columnIndex, ColEnabled)), result, rowNumber);
                    def.group = GetCellText(GetCell(row, columnIndex, ColGroup)).Trim();
                    def.sortOrder = (int)ParseFloat(GetCellText(GetCell(row, columnIndex, ColSortOrder)), result, rowNumber, ColSortOrder, "排序必须为整数");
                    def.serverVerifyOverride = (int)ParseVerify(GetCellText(GetCell(row, columnIndex, ColVerify)), result, rowNumber);
                    def.extra = GetCellText(GetCell(row, columnIndex, ColExtra)).Trim();

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
            finally
            {
                if (workbook != null) workbook.Close();
            }
        }

        private static ICell GetCell(IRow row, Dictionary<string, int> columnIndex, string column)
        {
            if (!columnIndex.TryGetValue(column, out int index)) return null;
            return row.GetCell(index);
        }

        private static string GetCellText(ICell cell)
        {
            if (cell == null) return string.Empty;
            switch (cell.CellType)
            {
                case CellType.String: return cell.StringCellValue ?? string.Empty;
                case CellType.Numeric:
                    double v = cell.NumericCellValue;
                    return v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("R");
                case CellType.Boolean: return cell.BooleanCellValue ? "1" : "0";
                case CellType.Formula:
                    try
                    {
                        switch (cell.CachedFormulaResultType)
                        {
                            case CellType.String: return cell.StringCellValue ?? string.Empty;
                            case CellType.Numeric: return cell.NumericCellValue.ToString("R");
                            default: return string.Empty;
                        }
                    }
                    catch { return string.Empty; }
                default: return string.Empty;
            }
        }

        private static bool IsRowEmpty(IRow row)
        {
            for (int c = row.FirstCellNum; c < row.LastCellNum; c++)
            {
                if (!string.IsNullOrEmpty(GetCellText(row.GetCell(c)))) return false;
            }
            return true;
        }

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

            // 移除重复行（保留首次出现），倒序删除避免索引错乱
            for (int i = removeAt.Count - 1; i >= 0; i--)
            {
                result.Products.RemoveAt(removeAt[i]);
                productRows.RemoveAt(removeAt[i]);
            }
        }

        /// <summary>返回 true 表示该值重复（重复行会被移出商品列表）。</summary>
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
            // JsonUtility 能解析任意合法 JSON 对象（未知字段自动忽略），非法 JSON 抛异常
            try { JsonUtility.FromJson<JsonProbe>(s); return true; }
            catch { return false; }
        }
    }
}

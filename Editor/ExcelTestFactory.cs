using System.Collections.Generic;
using System.IO;
using MiniExcelLibs;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// 测试辅助：用 MiniExcel 现场生成商品配置 Excel（供 EditMode 测试使用）。
    /// 放在 Editor 程序集里，测试程序集无需直接引用 MiniExcel。
    /// </summary>
    public static class ExcelTestFactory
    {
        /// <summary>
        /// 生成示例商品表：2 行合法 + 3 行非法
        /// （行3 缺内部ID；行4 订阅暂不支持；行5 Google ID 与行1 重复）。
        /// </summary>
        public static string CreateSampleExcel(string directory)
        {
            var rows = new List<IDictionary<string, object>>
            {
                Row("gem_100", "com.example.gem100", "com.example.gem100.ios", 0, "100 Gems", "", 1.99, "USD", 1, "currency", 1, -1, "{\"x\":1}"),
                Row("no_ads", "com.example.noads", "com.example.noads.ios", 1, "Remove Ads", "", 4.99, "USD", 1, "", 2, 0, ""),
                Row("", "com.example.bad1", "com.example.bad1.ios", 0, "", "", 0, "", 1, "", 0, -1, ""),
                Row("sub_test", "com.example.sub", "com.example.sub.ios", 2, "Sub", "", 0, "", 1, "", 0, -1, ""),
                Row("gem_200", "com.example.gem100", "com.example.gem200.ios", 0, "", "", 0, "", 1, "", 0, -1, ""),
            };
            string path = Path.Combine(directory, "products.xlsx");
            MiniExcel.SaveAs(path, rows, overwriteFile: true);
            return path;
        }

        /// <summary>生成一张全部合法的商品表（2 行），供生成流程测试使用。</summary>
        public static string CreateValidExcel(string directory)
        {
            var rows = new List<IDictionary<string, object>>
            {
                Row("gem_100", "com.example.gem100", "com.example.gem100.ios", 0, "100 Gems", "", 1.99, "USD", 1, "currency", 1, -1, "{\"x\":1}"),
                Row("no_ads", "com.example.noads", "com.example.noads.ios", 1, "Remove Ads", "", 4.99, "USD", 1, "", 2, 0, ""),
            };
            string path = Path.Combine(directory, "valid_products.xlsx");
            MiniExcel.SaveAs(path, rows, overwriteFile: true);
            return path;
        }

        /// <summary>生成一张缺少必填列的表（只有 Id_s / Title_s 两列）。</summary>
        public static string CreateBadHeaderExcel(string directory)
        {
            var rows = new List<IDictionary<string, object>>
            {
                new Dictionary<string, object> { ["Id_s"] = "x", ["Title_s"] = "y" },
            };
            string path = Path.Combine(directory, "bad_header.xlsx");
            MiniExcel.SaveAs(path, rows, overwriteFile: true);
            return path;
        }

        private static IDictionary<string, object> Row(string id, string google, string apple, int consumeType,
            string title, string description, double price, string currency, int enabled, string group,
            int sortOrder, int verify, string extra)
        {
            return new Dictionary<string, object>
            {
                ["Id_s"] = id,
                ["GoogleProductId_s"] = google,
                ["AppleProductId_s"] = apple,
                ["ConsumeType_i"] = consumeType,
                ["Title_s"] = title,
                ["Description_s"] = description,
                ["Price_f"] = price,
                ["Currency_s"] = currency,
                ["Enabled_i"] = enabled,
                ["Group_s"] = group,
                ["SortOrder_i"] = sortOrder,
                ["Verify_i"] = verify,
                ["Extra_s"] = extra,
            };
        }
    }
}

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
        /// 生成示例商品表：
        /// 行1/2 合法（消耗/非消耗）；行3 缺内部ID（警告跳过）；行4 类型非法（3，报错）；
        /// 行5 Google ID 重复；行6 ConsumeType=2 → 按映射成为非消耗型（合法）。
        /// </summary>
        public static string CreateSampleExcel(string directory)
        {
            var rows = new List<IDictionary<string, object>>
            {
                Row("gem_100", "com.example.gem100", "com.example.gem100.ios", 0, "100 Gems", "", 1.99, "USD", 1, "currency", 1, -1, "{\"x\":1}"),
                Row("no_ads", "com.example.noads", "com.example.noads.ios", 1, "Remove Ads", "", 4.99, "USD", 1, "", 2, 0, ""),
                Row("", "com.example.bad1", "com.example.bad1.ios", 0, "", "", 0, "", 1, "", 0, -1, ""),
                Row("bad_type", "com.example.bad2", "com.example.bad2.ios", 3, "", "", 0, "", 1, "", 0, -1, ""),
                Row("gem_200", "com.example.gem100", "com.example.gem200.ios", 0, "", "", 0, "", 1, "", 0, -1, ""),
                Row("sub_ok", "com.example.subok", "com.example.subok.ios", 2, "", "", 0, "", 1, "", 0, -1, ""),
            };
            string path = Path.Combine(directory, "products.xlsx");
            MiniExcel.SaveAs(path, rows, overwriteFile: true);
            return path;
        }

        /// <summary>生成含显式商店类型列（IapType_i）的表：IapType 优先于 ConsumeType 映射。</summary>
        public static string CreateExplicitTypeExcel(string directory)
        {
            var rows = new List<IDictionary<string, object>>
            {
                // ConsumeType=1（默认非消耗）但 IapType=0 → 消耗型（覆盖）
                new Dictionary<string, object> { ["Id_s"] = "a", ["GoogleProductId_s"] = "com.example.a", ["AppleProductId_s"] = "com.example.a.ios", ["ConsumeType_i"] = 1, ["IapType_i"] = 0 },
                // ConsumeType=0（默认消耗）但 IapType=2 → 订阅（覆盖）
                new Dictionary<string, object> { ["Id_s"] = "b", ["GoogleProductId_s"] = "com.example.b", ["AppleProductId_s"] = "com.example.b.ios", ["ConsumeType_i"] = 0, ["IapType_i"] = 2 },
                // 无 IapType（空值）→ 走 ConsumeType 映射：2 → 非消耗
                new Dictionary<string, object> { ["Id_s"] = "c", ["GoogleProductId_s"] = "com.example.c", ["AppleProductId_s"] = "com.example.c.ios", ["ConsumeType_i"] = 2, ["IapType_i"] = "" },
            };
            string path = Path.Combine(directory, "explicit_type.xlsx");
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

using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace CoffeeBean.Purchase.EditorTools
{
    /// <summary>
    /// 测试辅助：用 NPOI 现场生成商品配置 Excel（供 EditMode 测试使用）。
    /// 放在 Editor 程序集里，测试程序集无需直接引用 NPOI。
    /// </summary>
    public static class ExcelTestFactory
    {
        /// <summary>
        /// 生成示例商品表：2 行合法 + 3 行非法
        /// （行3 缺内部ID；行4 订阅暂不支持；行5 Google ID 与行1 重复）。
        /// </summary>
        public static string CreateSampleExcel(string directory)
        {
            string path = Path.Combine(directory, "products.xlsx");
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            using (var wb = new XSSFWorkbook())
            {
                ISheet sheet = wb.CreateSheet("Products");
                string[] header =
                {
                    "Id_s", "GoogleProductId_s", "AppleProductId_s", "ConsumeType_i",
                    "Title_s", "Description_s", "Price_f", "Currency_s",
                    "Enabled_i", "Group_s", "SortOrder_i", "Verify_i", "Extra_s"
                };
                IRow h = sheet.CreateRow(0);
                for (int i = 0; i < header.Length; i++) h.CreateCell(i).SetCellValue(header[i]);

                AddRow(sheet, 1, "gem_100", "com.example.gem100", "com.example.gem100.ios", 0,
                    "100 Gems", "", 1.99, "USD", 1, "currency", 1, -1, "{\"x\":1}");
                AddRow(sheet, 2, "no_ads", "com.example.noads", "com.example.noads.ios", 1,
                    "Remove Ads", "", 4.99, "USD", 1, "", 2, 0, "");
                AddRow(sheet, 3, "", "com.example.bad1", "com.example.bad1.ios", 0,
                    "", "", 0, "", 1, "", 0, -1, "");
                AddRow(sheet, 4, "sub_test", "com.example.sub", "com.example.sub.ios", 2,
                    "Sub", "", 0, "", 1, "", 0, -1, "");
                AddRow(sheet, 5, "gem_200", "com.example.gem100", "com.example.gem200.ios", 0,
                    "", "", 0, "", 1, "", 0, -1, "");

                wb.Write(fs);
            }
            return path;
        }

        /// <summary>生成一张全部合法的商品表（2 行），供生成流程测试使用。</summary>
        public static string CreateValidExcel(string directory)
        {
            string path = Path.Combine(directory, "valid_products.xlsx");
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            using (var wb = new XSSFWorkbook())
            {
                ISheet sheet = wb.CreateSheet("Products");
                string[] header =
                {
                    "Id_s", "GoogleProductId_s", "AppleProductId_s", "ConsumeType_i",
                    "Title_s", "Description_s", "Price_f", "Currency_s",
                    "Enabled_i", "Group_s", "SortOrder_i", "Verify_i", "Extra_s"
                };
                IRow h = sheet.CreateRow(0);
                for (int i = 0; i < header.Length; i++) h.CreateCell(i).SetCellValue(header[i]);

                AddRow(sheet, 1, "gem_100", "com.example.gem100", "com.example.gem100.ios", 0,
                    "100 Gems", "", 1.99, "USD", 1, "currency", 1, -1, "{\"x\":1}");
                AddRow(sheet, 2, "no_ads", "com.example.noads", "com.example.noads.ios", 1,
                    "Remove Ads", "", 4.99, "USD", 1, "", 2, 0, "");

                wb.Write(fs);
            }
            return path;
        }

        /// <summary>生成一张缺少必填列的表（只有 Id_s / Title_s 两列）。</summary>
        public static string CreateBadHeaderExcel(string directory)
        {
            string path = Path.Combine(directory, "bad_header.xlsx");
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            using (var wb = new XSSFWorkbook())
            {
                ISheet sheet = wb.CreateSheet("Products");
                IRow h = sheet.CreateRow(0);
                h.CreateCell(0).SetCellValue("Id_s");
                h.CreateCell(1).SetCellValue("Title_s");
                wb.Write(fs);
            }
            return path;
        }

        private static void AddRow(ISheet sheet, int rowIndex, params object[] cells)
        {
            IRow row = sheet.CreateRow(rowIndex);
            for (int i = 0; i < cells.Length; i++)
            {
                ICell cell = row.CreateCell(i);
                object v = cells[i];
                if (v is string s) cell.SetCellValue(s);
                else if (v is int n) cell.SetCellValue(n);
                else if (v is double d) cell.SetCellValue(d);
                else cell.SetCellValue(System.Convert.ToString(v));
            }
        }
    }
}

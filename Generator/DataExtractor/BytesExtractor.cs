using System.Data;
using ExcelParser;
using Common.Utils;
using ClosedXML.Excel;

namespace Generator.DataExtractor;

public class BytesExtractor : DataExtractorBase
{
    public BytesExtractor(string directory) : base(directory, "bin")
    {
    }

    protected override List<CustomDataTable> GetDataTablesByFile(string filePath)
    {
        List<CustomDataTable> tables = new List<CustomDataTable>();
        using (var workbook = new XLWorkbook(filePath))
        {
            if (workbook.Worksheets.Count == 0)
            {
                Logger.Instance.Error($"Fail to load. (file path : {filePath})");
                return tables;
            }
            Logger.Instance.Log($"Success to load. (file path : {filePath})");
            
            foreach (var workSheet in workbook.Worksheets)
            {
                Logger.Instance.Log($"Start Convert Worksheet to DataTable({workSheet.Name})");
                
                // CustomDataTable 사용
                var table = TableHelper.GetTable(workSheet);
                tables.Add(TableHelper.GetTable(workSheet));
                Logger.Instance.Log($"Finish Convert Worksheet to DataTable({workSheet.Name})");
            }
        }
        return tables;
    }

    protected override void SaveData(CustomDataTable table, string path)
    {
        // JSON 형태로 변환 후 바이트로 저장
        var json = TableHelper.ConvertDataTableToJSON(table);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        
        // 파일 저장
        File.WriteAllBytes(path, bytes);
        Logger.Instance.Log($"Save File Byte {path} ({table.RowCount} rows, {table.ColumnCount} columns)");
    }
}

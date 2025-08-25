using System.Data;
using ExcelParser;
using Common.Utils;

namespace Generator.DataExtractor;

public class BytesExtractor : DataExtractorBase
{
    public BytesExtractor(string directory) : base(directory, "bin")
    {
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

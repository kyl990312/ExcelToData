using ExcelParser;
using System.Data;
using Common.Utils;

namespace Generator.DataExtractor;

public class JsonExtractor : DataExtractorBase
{
    public JsonExtractor(string directory) : base(directory, "json")
    {
    }

    protected override void SaveData(CustomDataTable table, string path)
    {
        // JSON 문자열로 변환
        var json = TableHelper.ConvertDataTableToJSON(table);
        
        // 파일 저장 (보기 좋게 포매팅)
        var formattedJson = System.Text.Json.JsonSerializer.Serialize(
            System.Text.Json.JsonSerializer.Deserialize<object>(json), 
            new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        
        File.WriteAllText(path, formattedJson);
        Logger.Instance.Log($"Save File Json {path} ({table.RowCount} rows, {table.ColumnCount} columns)");
    }
}

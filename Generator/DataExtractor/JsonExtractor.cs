using ExcelParser;
using Common.Utils;

using BinaryReader = BinaryParser.BinaryReader;

namespace Generator.DataExtractor;

/// <summary>
/// SturctedBinaryExtractor로 뽑은 바이너리 파일에서 테이블 정보를 읽어 JSon으로 변형해 저장하는 클래스.
/// </summary>
public class JsonExtractor : DataExtractorBase
{
    public JsonExtractor(string directory) : base(directory, "json")
    {
    }

    protected override List<CustomDataTable> GetDataTablesByFile(string filePath)
    {
        var tables = new List<CustomDataTable>();
        var binaryReader = new BinaryReader();
        var table = TableHelper.ConvertToCustomDataTable(binaryReader.ReadFromFile(filePath), Path.GetFileNameWithoutExtension(filePath));
        tables.Add(table);
        return tables;
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
        Logger.Instance.Log($"[JsonExtractor] Save File Json {path} ({table.RowCount} rows, {table.ColumnCount} columns)");
    }
}

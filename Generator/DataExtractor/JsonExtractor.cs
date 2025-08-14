using ExcelParser;
using System.Data;
using Common.Utils;

using BinaryReader = BinaryParser.BinaryReader;

namespace Generator.DataExtractor;

public class JsonExtractor : DataExtractorBase
{
    private BinaryReader _binaryReader = new();
    public JsonExtractor(string directory) : base(directory, "json")
    {
    }
    
    protected override List<(DataTable table, string name)> ConvertToDataTable(string filePath)
    {
        Logger.Instance.Log($"[JsonExtractor] Convert to DataTable {filePath}");
        // directory 내에 있는 모든 .bin 파일을 읽어와 DataTable로 변환합니다.
        List<(DataTable table, string name)> tables = new List<(DataTable table, string name)>();
        try
        {
            var result = _binaryReader.ReadFromFile(filePath);
            var dataTable = TableHelper.ConvertBinaryToDataTable(result);
            tables.Add((dataTable, Path.GetFileNameWithoutExtension(filePath)));
        }
        catch (Exception e)
        {
            Logger.Instance.Log($"[JsonExtractor] Fail to read binary file: {filePath} - {e.Message}\n{e.StackTrace}");
        }
        
        return tables;
    }

    protected override void SaveData(DataTable table, string path)
    {
        // 바이트 변환
        var json = TableHelper.ConverDataTableToJSON(table);
        
        // 파일 저장
        File.WriteAllText(path, json);
        Logger.Instance.Log($"Save File Json {path}");
    }
}

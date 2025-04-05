using ExcelParser;
using System.Data;
using Common.Utils;

namespace Generator.DataExtractor;

public class JsonExtractor : DataExtractorBase
{
    public JsonExtractor(string directory) : base(directory, "json")
    {
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
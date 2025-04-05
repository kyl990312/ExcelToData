using System.Data;
using ExcelParser;
using Common.Utils;

namespace Generator.DataExtractor;
public class BytesExtractor : DataExtractorBase
{
    public BytesExtractor(string directory) : base(directory, "bin")
    {
    }

    protected override void SaveData(DataTable table, string path)
    {
        // 바이트 변환
        var json = TableHelper.ConverDataTableToJSON(table);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        
        // 파일 저장
        File.WriteAllBytes(path, bytes);
        Logger.Instance.Log($"Save File Byte {path}");
    }
}
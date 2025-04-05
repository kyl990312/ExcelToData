using System.Data;
using ClosedXML.Excel;
using System.Text.Json;
using ExcelParser;
using Common.Utils;

/**
 * * 동일 엑셀 파일에 존재하는 sheet는 한 코드 파일에 작성합니다.
 */
namespace ExcelToData.EnumGenerator;

public class EnumGenerator
{
    private EnumGeneratorConfig _config;
    public EnumGenerator(string configPath)
    {
        var json = File.ReadAllText(configPath);
        _config = JsonSerializer.Deserialize<EnumGeneratorConfig>(json);
    }
    
    public void Generate()
    {
        var template = GetTemplate(_config.TemplatePath);
        using (var workbook = new XLWorkbook(_config.FromFilePath))
        {
            foreach (var worksheet in workbook.Worksheets)
            {
                var table = TableHelper.GetTable(worksheet);
                var name = worksheet.Name;
            }
        }
    }

    private string[] GetEnumValues(DataTable table, string nameColumn, string valueColunm)
    {
        var result = new List<Tuple<string,int>>();

        foreach (DataRow row in table.Rows)
        {
            result.Add(new Tuple<string, int>(row[nameColumn].ToString(), Convert.ToInt32(row[valueColunm])));
        }
        
        result.Sort((a,b)=>a.Item2.CompareTo(b.Item2));

        return result.Select(x => x.Item1).ToArray();
    }
    
    private string GetTemplate(string filePath)
    {
        return File.ReadAllText(filePath);
    }
    
    private void Save(string script)
    {
        var directoryPath = Path.GetDirectoryName(_config.ToDirectory);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            Logger.Instance.Log($"Create Directory {directoryPath}");
        }

        var fullPath = $"{_config.ToDirectory}.{_config.Exetension}";
        File.WriteAllText(fullPath, script);
        Logger.Instance.Log($"Save Enum Script File {fullPath}");
    }
}
using System.Data;
using ClosedXML.Excel;
using Common.Utils;
using ExcelParser;

namespace Generator.DataExtractor;

public interface IDataExtractor
{
    void ExtractData(string filePath);
}

public abstract class DataExtractorBase : IDataExtractor
{
    public string ResultDirectoryPath { get; private set; }
    private string _extend;

    public DataExtractorBase(string toDirectory, string extend)
    {
        ResultDirectoryPath = toDirectory;
        _extend = extend;
    }
    
    public void ExtractData(string filePath)
    {
        using (var workbook = new XLWorkbook(filePath))
        {
            if (workbook.Worksheets.Count == 0)
            {
                Logger.Instance.Error($"Fail to load. (file path : {filePath})");
                return;
            }
            Logger.Instance.Log($"Success to load. (file path : {filePath})");
            
            foreach (var workSheet in workbook.Worksheets)
            {
                Logger.Instance.Log($"Start Convert Worksheet ({workSheet.Name})");
                
                // CustomDataTable 사용
                var table = TableHelper.GetTable(workSheet);
                var path = GetFilePath(workSheet.Name, _extend);
                
                PostProcessSaveData(table, path);
                SaveData(table, path);
                
                Logger.Instance.Log($"Finish Convert Worksheet ({workSheet.Name})");
            }
        }
    }

    /// <summary>
    /// CustomDataTable을 저장하는 추상 메서드
    /// </summary>
    protected abstract void SaveData(CustomDataTable table, string path);
    
    private string GetFilePath(string fileName, string extend, string middle = "")
    {
        if (!middle.Equals("")) return ResultDirectoryPath + "/" + middle + "/" + fileName + "." + extend;
        return ResultDirectoryPath + "/" + fileName + "." + extend;
    }

    private void PostProcessSaveData(CustomDataTable table, string path)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            Logger.Instance.Log($"Create Directory {directoryPath}");
        }
    }
}

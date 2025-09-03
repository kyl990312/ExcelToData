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
        var dataDataTables = GetDataTablesByFile(filePath);
        foreach (var table in dataDataTables)
        {
            var path = GetFilePath(table.Name, _extend);
                
            PostProcessSaveData(table, path);
            SaveData(table, path);
        }
    }

    protected abstract List<CustomDataTable> GetDataTablesByFile(string filePath);
    
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


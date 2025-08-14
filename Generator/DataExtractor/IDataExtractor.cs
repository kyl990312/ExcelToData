using System.Data;
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
        var tableTupleList = ConvertToDataTable(filePath);
        foreach (var tableTuple in tableTupleList)
        {
            var path = GetFilePath(tableTuple.name, _extend);
            PostProcessSaveData(tableTuple.table,path);
            SaveData(tableTuple.table, GetFilePath(tableTuple.name, _extend));
            Logger.Instance.Log($"Finish Extract Data ({tableTuple.name})");
        }
    }

    protected abstract void SaveData(DataTable table, string path);
    
    protected abstract List<(DataTable table, string name)> ConvertToDataTable(string path);
    
    protected string GetFilePath(string fileName, string extend, string middle = "")
    {
        if (!middle.Equals("")) return ResultDirectoryPath + "/" + middle + "/" + fileName + "." + extend;
        return ResultDirectoryPath + "/" + fileName + "." + extend;
    }

    private void PostProcessSaveData(DataTable table, string path)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            Logger.Instance.Log($"Create Directory {directoryPath}");
        }
    }
}

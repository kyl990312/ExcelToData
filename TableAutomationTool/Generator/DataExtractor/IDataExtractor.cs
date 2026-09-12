using TableAutomationTool.Common.Utils;
using TableAutomationTool.ExcelParser;

namespace TableAutomationTool.Generator.DataExtractor;

public interface IDataExtractor
{
    /// <param name="tableFilter">
    /// 추출할 테이블(워크시트) 이름 집합. null 이면 워크북 안의 테이블을 전부 추출한다.
    /// </param>
    void ExtractData(string filePath, IReadOnlyCollection<string>? tableFilter = null);
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
    
    public void ExtractData(string filePath, IReadOnlyCollection<string>? tableFilter = null)
    {
        var dataDataTables = GetDataTablesByFile(filePath);

        // 테이블의 단위는 워크북이 아니라 워크시트다. 특정 테이블만 지정되면 그것만 저장한다.
        if (tableFilter is { Count: > 0 })
        {
            var wanted = new HashSet<string>(tableFilter, StringComparer.OrdinalIgnoreCase);
            var before = dataDataTables.Count;
            dataDataTables = dataDataTables.Where(t => wanted.Contains(t.Name)).ToList();
            Logger.Instance.Log(
                $"[DataExtractor] 테이블 필터 적용: {dataDataTables.Count}/{before} ({Path.GetFileName(filePath)})");
        }

        foreach (var table in dataDataTables)
        {
            var path = GetFilePath(table.Name, _extend);
            PostProcessSaveData(table, path);
            SaveData(table, path);
        }
    }

    protected abstract List<CustomDataTable> GetDataTablesByFile(string filePath);
    
    /// <summary>
    /// CustomDataTable�� �����ϴ� �߻� �޼���
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


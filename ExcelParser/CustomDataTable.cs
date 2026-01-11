using Common.Utils;
using System.Text.Json;
using ExcelParser.Core;

namespace ExcelParser;

/// <summary>
/// DataTable을 대체하는 사용자 정의 테이블 클래스
/// Dictionary 형식의 데이터만 처리
/// </summary>
public class CustomDataTable
{
    private TableHeader _header;
    private Dictionary<string, List<object>> _data = new();

    public string Name
    {
        get => _header.TableName;
        set => _header.TableName = value;
    }

    /// <summary>
    /// 칼럼 타입 정보와 데이터로부터 CustomDataTable을 생성합니다.
    /// </summary>
    /// <param name="tableName">테이블명</param>
    /// <param name="columnTypes">칼럼명과 타입 문자열 딕셔너리</param>
    /// <param name="data">행 데이터 리스트</param>
    public CustomDataTable(string tableName, Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data)
    {
        _header = new TableHeader(tableName, columnTypes);
        ExtractData(data);
    }

    /// <summary>
    /// 빈 테이블을 생성합니다.
    /// </summary>
    /// <param name="tableName">테이블명</param>
    public CustomDataTable(string tableName)
    {
        _header = new TableHeader(tableName, new Dictionary<string, string>());
        _data = new Dictionary<string, List<object>>();
    }

    /// <summary>
    /// Dictionary 리스트로부터 데이터를 추출하여 테이블에 저장
    /// </summary>
    private void ExtractData(List<Dictionary<string, object>> data)
    {
        // 칼럼별 데이터 초기화
        foreach (var name in _header.Names)
        {
            _data[name] = new List<object>();
        }

        if (data == null || data.Count == 0)
        {
            Logger.Instance.Warning($"[CustomDataTable] Empty data provided for table '{Name}'");
            return;
        }

        // 데이터 추출
        foreach (var rowData in data)
        {
            foreach (var columnName in _header.Names)
            {
                if (rowData.TryGetValue(columnName, out var value))
                {
                    _data[columnName].Add(value);
                }
                else
                {
                    var dataType = _header.GetColumnDataType(columnName);
                    var customTypeName = _header.GetColumnCustomTypeName(columnName);
                    var defaultValue = DataTypeHelper.GetDefaultValue(dataType, false, customTypeName);
                    _data[columnName].Add(defaultValue);
                }
            }
        }

        Logger.Instance.Log($"[CustomDataTable] Extracted {RowCount} rows with {ColumnCount} columns for table '{Name}'");
    }

    public int RowCount => _data.Values.FirstOrDefault()?.Count ?? 0;
    public int ColumnCount => _header.ColumnCount;
    public IEnumerable<string> ColumnNames => _header.Names;
    public IEnumerable<string> ColumnTypeStrings => _header.TypeStrings;
    public TableHeader Header => _header;

    /// <summary>
    /// 칼럼 타입 정보를 Dictionary로 반환합니다.
    /// </summary>
    public Dictionary<string, string> GetColumnTypes()
    {
        var columnTypes = new Dictionary<string, string>();
        foreach (var header in _header.Headers)
        {
            columnTypes[header.name] = header.typeString;
        }
        return columnTypes;
    }

    public List<object> GetColumnData(string columnName)
    {
        return _data.TryGetValue(columnName, out var columnData) ? columnData : new List<object>();
    }

    public T GetValue<T>(string columnName, int rowIndex)
    {
        if (!_data.TryGetValue(columnName, out var columnData) ||
            rowIndex < 0 || rowIndex >= columnData.Count)
            return default;

        try
        {
            return (T)columnData[rowIndex];
        }
        catch (InvalidCastException)
        {
            Logger.Instance.Warning($"[CustomDataTable] Cannot cast value at [{columnName}][{rowIndex}] to type {typeof(T)}");
            return default;
        }
    }

    public object GetValue(string columnName, int rowIndex)
    {
        if (!_data.TryGetValue(columnName, out var columnData) ||
            rowIndex < 0 || rowIndex >= columnData.Count)
            return null;

        return columnData[rowIndex];
    }

    public Dictionary<string, object> GetRowData(int rowIndex)
    {
        var rowData = new Dictionary<string, object>();

        if (rowIndex < 0 || rowIndex >= RowCount)
            return rowData;

        foreach (var columnName in _header.Names)
        {
            if (_data.TryGetValue(columnName, out var columnData))
            {
                rowData[columnName] = columnData[rowIndex];
            }
        }

        return rowData;
    }

    public List<Dictionary<string, object>> GetAllRowsData()
    {
        var allRows = new List<Dictionary<string, object>>();

        for (int i = 0; i < RowCount; i++)
        {
            allRows.Add(GetRowData(i));
        }

        return allRows;
    }

    public string ToJson()
    {
        var allRowsData = GetAllRowsData();
        return JsonSerializer.Serialize(allRowsData, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    public bool HasColumn(string columnName) => _header.HasColumn(columnName);
    public string GetColumnTypeString(string columnName) => _header.GetColumnTypeString(columnName);

    public void PrintTableInfo()
    {
        Logger.Instance.Log($"[CustomDataTable] Table Info: {Name} - {RowCount} rows, {ColumnCount} columns");
        foreach (var group in _header.Headers)
        {
            var columnData = GetColumnData(group.name);
            Logger.Instance.Log($"[CustomDataTable]   Column [{group.name}] ({group.typeString}): {columnData.Count} values");
        }
    }
}

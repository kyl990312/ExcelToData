using ClosedXML.Excel;
using Common.Utils;
using System.Text.Json;
using ExcelParser.Core;

namespace ExcelParser;

/// <summary>
/// DataTable을 대체하는 사용자 정의 테이블 클래스
/// 사용자 정의 Enum 지원을 위해 설계됨
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

    public CustomDataTable(IXLWorksheet worksheet)
    {
        _header = new TableHeader(worksheet);
        ExtractData(worksheet);
    }

    /// <summary>
    /// Dictionary 리스트로부터 CustomDataTable을 생성합니다.
    /// </summary>
    public CustomDataTable(List<Dictionary<string, object>> data, string tableName = "DataTable")
    {
        if (data == null || data.Count == 0)
        {
            Logger.Instance.Warning("Empty data provided to CustomDataTable constructor");
            _header = new TableHeader(tableName, new Dictionary<string, Type>());
            return;
        }

        var firstRow = data[0];
        var columnTypes = new Dictionary<string, Type>();

        foreach (var kvp in firstRow)
        {
            Type valueType = kvp.Value?.GetType() ?? typeof(string);
            columnTypes[kvp.Key] = valueType;
        }

        _header = new TableHeader(tableName, columnTypes);
        ExtractDataFromDictionaries(data);
    }

    /// <summary>
    /// 워크시트에서 데이터를 추출하여 테이블에 저장
    /// </summary>
    private void ExtractData(IXLWorksheet worksheet)
    {
        foreach (var name in _header.Names)
        {
            _data[name] = new List<object>();
        }

        var rows = TableHelper.GetRows(worksheet, out var startIdx, out var endIdx);

        for (int i = 0; i < _header.HeaderRowCount; i++)
        {
            if (rows.Count > 0)
                rows.RemoveAt(0);
        }

        foreach (var columnIndex in _header.ColumnIndices)
        {
            var headerGroup = _header[columnIndex];
            var columnName = headerGroup.name;
            var columnType = headerGroup.type;
            var dataType = headerGroup.dataType;
            var customTypeName = headerGroup.customTypeName;
            var isArray = headerGroup.isArray;

            foreach (var row in rows)
            {
                var cell = row.Cell(columnIndex + 1);
                try
                {
                    var convertedValue = TableHelper.GetConvertedCellValue(cell, columnType, dataType, customTypeName, isArray);
                    _data[columnName].Add(convertedValue);
                }
                catch (Exception e)
                {
                    Logger.Instance.Error($"[CustomDataTable] Error converting column {columnName} - {cell.Address.RowNumber}: {e.Message}");
                    _data[columnName].Add(DataTypeHelper.GetDefaultValue(dataType, isArray));
                }
            }
        }
        Logger.Instance.Log($"Extracted {RowCount} rows with {ColumnCount} columns from worksheet");
    }

    /// <summary>
    /// Dictionary 리스트로부터 데이터를 추출하여 테이블에 저장
    /// </summary>
    private void ExtractDataFromDictionaries(List<Dictionary<string, object>> data)
    {
        foreach (var name in _header.Names)
        {
            _data[name] = new List<object>();
        }

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
                    var columnType = _header.GetColumnType(columnName);
                    var defaultValue = DataTypeHelper.GetDefaultValue(columnType);
                    _data[columnName].Add(defaultValue);
                    Logger.Instance.Warning($"Column '{columnName}' not found in row data, using default value");
                }
            }
        }

        Logger.Instance.Log($"Extracted {RowCount} rows with {ColumnCount} columns from dictionary list");
    }

    public int RowCount => _data.Values.FirstOrDefault()?.Count ?? 0;
    public int ColumnCount => _header.ColumnCount;
    public IEnumerable<string> ColumnNames => _header.Names;
    public IEnumerable<Type> ColumnTypes => _header.Types;
    public TableHeader Header => _header;

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
            Logger.Instance.Warning($"Cannot cast value at [{columnName}][{rowIndex}] to type {typeof(T)}");
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
    public Type GetColumnType(string columnName) => _header.GetColumnType(columnName);

    public void PrintTableInfo()
    {
        Logger.Instance.Log($"Table Info: {RowCount} rows, {ColumnCount} columns");
        foreach (var group in _header.Headers)
        {
            var columnData = GetColumnData(group.name);
            Logger.Instance.Log($"Column [{group.name}] ({group.type}): {columnData.Count} values");
        }
    }
}

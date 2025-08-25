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

    public CustomDataTable(IXLWorksheet worksheet)
    {
        _header = new TableHeader(worksheet);
        ExtractData(worksheet);
    }

    /// <summary>
    /// 워크시트에서 데이터를 추출하여 테이블에 저장
    /// </summary>
    private void ExtractData(IXLWorksheet worksheet)
    {
        // 1. 컬럼별 데이터 리스트 초기화
        foreach (var name in _header.Names)
        {
            _data[name] = new List<object>();
        }

        // 2. 유효한 행들 가져오기 (주석, 빈 행 제외)
        var rows = TableHelper.GetRows(worksheet, out var startIdx, out var endIdx);
        
        // 3. 헤더 행들 제거 (컬럼명, 타입 정의 행)
        for (int i = 0; i < _header.HeaderRowCount; i++)
        {
            if (rows.Count > 0)
                rows.RemoveAt(0);
        }

        // 4. 각 행의 데이터 추출
        foreach (var row in rows)
        {
            foreach (var columnIndex in _header.ColumnIndices)
            {
                var headerGroup = _header[columnIndex];
                var columnName = headerGroup.name;
                var columnType = headerGroup.type;
                
                // 셀 값을 해당 타입으로 변환
                var cell = row.Cell(columnIndex + 1); // ClosedXML은 1-based 인덱스
                var convertedValue = TableHelper.GetConvertedCellValue(cell, columnType);
                
                // 변환된 값을 해당 컬럼 데이터에 추가
                _data[columnName].Add(convertedValue);
            }
        }

        Logger.Instance.Log($"Extracted {RowCount} rows with {ColumnCount} columns from worksheet");
    }

    // 프로퍼티들
    public int RowCount => _data.Values.FirstOrDefault()?.Count ?? 0;
    public int ColumnCount => _header.ColumnCount;
    public IEnumerable<string> ColumnNames => _header.Names;
    public IEnumerable<Type> ColumnTypes => _header.Types;
    public TableHeader Header => _header;

    // 데이터 접근 메서드들
    public List<object> GetColumnData(string columnName)
    {
        return _data.TryGetValue(columnName, out var columnData) ? columnData : new List<object>();
    }

    public T GetValue<T>(string columnName, int rowIndex)
    {
        if (!_data.TryGetValue(columnName, out var columnData) || 
            rowIndex < 0 || rowIndex >= columnData.Count)
            return default(T);
        
        try
        {
            return (T)columnData[rowIndex];
        }
        catch (InvalidCastException)
        {
            Logger.Instance.Warning($"Cannot cast value at [{columnName}][{rowIndex}] to type {typeof(T)}");
            return default(T);
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

    /// <summary>
    /// 모든 행 데이터를 Dictionary 리스트로 반환 (JSON 변환 용도)
    /// </summary>
    public List<Dictionary<string, object>> GetAllRowsData()
    {
        var allRows = new List<Dictionary<string, object>>();
        
        for (int i = 0; i < RowCount; i++)
        {
            allRows.Add(GetRowData(i));
        }
        
        return allRows;
    }

    /// <summary>
    /// 테이블을 JSON 문자열로 변환
    /// </summary>
    public string ToJson()
    {
        var allRowsData = GetAllRowsData();
        return JsonSerializer.Serialize(allRowsData, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <summary>
    /// 컬럼이 존재하는지 확인
    /// </summary>
    public bool HasColumn(string columnName)
    {
        return _header.HasColumn(columnName);
    }

    /// <summary>
    /// 컬럼의 타입을 반환
    /// </summary>
    public Type GetColumnType(string columnName)
    {
        return _header.GetColumnType(columnName);
    }

    /// <summary>
    /// 디버그용 테이블 정보 출력
    /// </summary>
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

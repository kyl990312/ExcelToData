using ClosedXML.Excel;
using TableAutomationTool.Common.Utils;

namespace TableAutomationTool.ExcelParser;

/// <summary>
/// 다양한 테이블 형식을 Dictionary 형식으로 변환하는 클래스
/// </summary>
public static class TableConverter
{
    private const int HeaderRowCount = 2; // 헤더 행 수 (칼럼명 + 타입)

    /// <summary>
    /// Excel 워크시트를 Dictionary 리스트로 변환합니다.
    /// </summary>
    public static (string tableName, Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data) 
        ConvertFromWorksheet(IXLWorksheet worksheet)
    {
        var tableName = worksheet.Name;
        var columnTypes = new Dictionary<string, string>();
        var data = new List<Dictionary<string, object>>();

        var rows = GetValidRowsFromWorksheet(worksheet);
        
        if (rows.Count < HeaderRowCount)
        {
            Logger.Instance.Warning($"[TableConverter] Worksheet '{tableName}' has insufficient rows");
            return (tableName, columnTypes, data);
        }

        // 헤더 파싱 (첫 번째 행: 칼럼명, 두 번째 행: 타입)
        var names = rows[0].Cells()?.ToList();
        var types = rows[1].Cells()?.ToList();
        
        if (names == null || names.Count == 0)
        {
            Logger.Instance.Error($"[TableConverter] No column names found in worksheet '{tableName}'");
            return (tableName, columnTypes, data);
        }

        // 유효한 칼럼 정보 수집
        var validColumns = GetValidColumns(
            names.Select(c => c?.Value.ToString() ?? "").ToArray(),
            types?.Select(c => c?.Value.ToString() ?? "String").ToArray() ?? Array.Empty<string>(),
            columnTypes);

        if (validColumns.Count == 0)
        {
            Logger.Instance.Warning($"[TableConverter] No valid columns found in worksheet '{tableName}'");
            return (tableName, columnTypes, data);
        }

        // 데이터 행 변환 (헤더 행 제외)
        for (int rowIndex = HeaderRowCount; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowDict = new Dictionary<string, object>();

            foreach (var (colIndex, colName) in validColumns)
            {
                var cell = row.Cell(colIndex + 1);
                rowDict[colName] = GetCellValue(cell);
            }

            data.Add(rowDict);
        }

        Logger.Instance.Log($"[TableConverter] Converted worksheet '{tableName}': {validColumns.Count} columns, {data.Count} rows");
        return (tableName, columnTypes, data);
    }

    /// <summary>
    /// Google Sheets 데이터(string[][] 형식)를 Dictionary 리스트로 변환합니다.
    /// </summary>
    public static (Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data) 
        ConvertFromStringArray(string tableName, List<string[]> table)
    {
        var columnTypes = new Dictionary<string, string>();
        var data = new List<Dictionary<string, object>>();

        if (table == null || table.Count == 0)
        {
            Logger.Instance.Warning($"[TableConverter] Table is null or empty: {tableName}");
            return (columnTypes, data);
        }

        var rows = GetValidRowsFromStringArray(table);

        if (rows.Count < HeaderRowCount)
        {
            Logger.Instance.Warning($"[TableConverter] Table '{tableName}' has insufficient rows");
            return (columnTypes, data);
        }

        // 유효한 칼럼 정보 수집
        var validColumns = GetValidColumns(rows[0], rows[1], columnTypes);

        if (validColumns.Count == 0)
        {
            Logger.Instance.Warning($"[TableConverter] No valid columns found in table: {tableName}");
            return (columnTypes, data);
        }

        // 데이터 행 변환 (헤더 행 제외)
        for (int rowIndex = HeaderRowCount; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowDict = new Dictionary<string, object>();

            foreach (var (colIndex, colName) in validColumns)
            {
                var cellValue = colIndex < row.Length ? row[colIndex] : string.Empty;
                rowDict[colName] = cellValue;
            }

            data.Add(rowDict);
        }

        Logger.Instance.Log($"[TableConverter] Converted table '{tableName}': {validColumns.Count} columns, {data.Count} rows");
        return (columnTypes, data);
    }

    /// <summary>
    /// 유효한 칼럼 정보를 수집합니다.
    /// </summary>
    private static Dictionary<int, string> GetValidColumns(string[] names, string[] types, Dictionary<string, string> columnTypes)
    {
        var validColumns = new Dictionary<int, string>();

        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i] ?? "";
            var typeStr = (i < types.Length) ? types[i] ?? "String" : "String";

            // 빈 칼럼명 스킵
            if (string.IsNullOrEmpty(name))
                continue;

            // 제외 칼럼 스킵
            if (name.StartsWith(TableDefine.REGEX_START_EXCEPT_COLUMN))
                continue;

            // 중복 칼럼 스킵
            if (columnTypes.ContainsKey(name))
                continue;

            columnTypes[name] = typeStr;
            validColumns[i] = name;
        }

        return validColumns;
    }

    /// <summary>
    /// 행이 유효한지 검사합니다.
    /// </summary>
    private static bool IsValidRow(string firstCellValue)
    {
        // EOF 처리
        if (firstCellValue.Equals("EOF", StringComparison.OrdinalIgnoreCase))
            return false;

        // 제외 행 스킵
        if (firstCellValue.StartsWith(TableDefine.REGEX_START_EXCEPT_ROW))
            return false;

        return true;
    }

    /// <summary>
    /// 워크시트에서 유효한 행만 수집합니다.
    /// </summary>
    private static List<IXLRow> GetValidRowsFromWorksheet(IXLWorksheet sheet)
    {
        var result = new List<IXLRow>();

        foreach (var row in sheet.Rows())
        {
            // 빈 행 스킵
            if (row.CellCount() == 0)
                continue;

            var firstCell = row.Cells().FirstOrDefault();
            if (firstCell == null)
                continue;

            var firstValue = firstCell.Value.ToString();

            // EOF면 종료
            if (firstValue.Equals("EOF", StringComparison.OrdinalIgnoreCase))
                break;

            // 유효성 검사
            if (!IsValidRow(firstValue))
                continue;

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// string 배열에서 유효한 행만 수집합니다.
    /// </summary>
    private static List<string[]> GetValidRowsFromStringArray(List<string[]> table)
    {
        var result = new List<string[]>();

        foreach (var row in table)
        {
            // 빈 행 스킵
            if (row.Length == 0 || row.All(cell => string.IsNullOrWhiteSpace(cell)))
                continue;

            var firstValue = row[0] ?? "";

            // EOF면 종료
            if (firstValue.Equals("EOF", StringComparison.OrdinalIgnoreCase))
                break;

            // 유효성 검사
            if (!IsValidRow(firstValue))
                continue;

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// 셀 값을 object로 반환합니다.
    /// </summary>
    private static object GetCellValue(IXLCell cell)
    {
        if (cell.Value.IsNumber)
            return cell.Value.GetNumber();
        if (cell.Value.IsBoolean)
            return cell.Value.GetBoolean();
        if (cell.Value.IsDateTime)
            return cell.Value.GetDateTime();
        if (cell.Value.IsText)
            return cell.Value.GetText();
        return cell.Value.ToString();
    }
}

using ClosedXML.Excel;
using TableAutomation.Common.Utils;

namespace ExcelParser;

/// <summary>
/// 테이블 관련 유틸리티 메서드를 제공하는 헬퍼 클래스
/// </summary>
public static class TableHelper
{
    /// <summary>
    /// Excel 워크시트로부터 CustomDataTable을 생성합니다
    /// </summary>
    public static CustomDataTable GetTable(IXLWorksheet sheet)
    {
        var (tableName, columnTypes, data) = TableConverter.ConvertFromWorksheet(sheet);
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// Google Sheets 데이터로부터 CustomDataTable을 생성합니다
    /// </summary>
    public static CustomDataTable GetTable(string tableName, List<string[]> table)
    {
        if (table == null)
        {
            Logger.Instance.Error("[TableHelper] Input table is null");
            return new CustomDataTable(tableName);
        }

        if (table.Count == 0)
        {
            Logger.Instance.Warning($"[TableHelper] Table is empty: {tableName}");
            return new CustomDataTable(tableName);
        }

        var (columnTypes, data) = TableConverter.ConvertFromStringArray(tableName, table);
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// Dictionary 리스트로부터 CustomDataTable을 생성합니다
    /// </summary>
    public static CustomDataTable GetTable(string tableName, Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data)
    {
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// CustomDataTable을 JSON 문자열로 변환합니다.
    /// </summary>
    public static string ConvertDataTableToJSON(CustomDataTable table)
    {
        return table.ToJson();
    }
}

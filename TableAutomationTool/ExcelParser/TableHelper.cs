using ClosedXML.Excel;
using TableAutomationTool.Common.Utils;

namespace TableAutomationTool.ExcelParser;

/// <summary>
/// ���̺� ���� ��ƿ��Ƽ �޼��带 �����ϴ� ���� Ŭ����
/// </summary>
public static class TableHelper
{
    /// <summary>
    /// Excel ��ũ��Ʈ�κ��� CustomDataTable�� �����մϴ�
    /// </summary>
    public static CustomDataTable GetTable(IXLWorksheet sheet)
    {
        var (tableName, columnTypes, data) = TableConverter.ConvertFromWorksheet(sheet);
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// 워크북 안의 테이블(워크시트) 이름 목록을 돌려준다.
    ///
    /// 추출 결과 .bin 은 "워크북 이름"이 아니라 "워크시트 이름"으로 저장된다
    /// (DataExtractorBase 가 table.Name 으로 파일을 만든다). 즉 테이블의 단위는 워크시트다.
    /// 목록 UI 나 -each 해석이 이 사실을 따라야 하므로 한 곳으로 모아 둔다.
    /// 열 수 없는 파일은 빈 목록을 돌려주고 로그만 남긴다.
    /// </summary>
    public static IReadOnlyList<string> GetTableNames(string workbookPath)
    {
        try
        {
            using var workbook = new XLWorkbook(workbookPath);
            return workbook.Worksheets.Select(s => s.Name).ToList();
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"[TableHelper] 워크북을 열 수 없습니다: {workbookPath} ({ex.Message})");
            return Array.Empty<string>();
        }
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
    /// Dictionary ����Ʈ�κ��� CustomDataTable�� �����մϴ�
    /// </summary>
    public static CustomDataTable GetTable(string tableName, Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data)
    {
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// CustomDataTable�� JSON ���ڿ��� ��ȯ�մϴ�.
    /// </summary>
    public static string ConvertDataTableToJSON(CustomDataTable table)
    {
        return table.ToJson();
    }
}

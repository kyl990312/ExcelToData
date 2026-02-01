using ClosedXML.Excel;
using TableAutomation.Common.Utils;

namespace ExcelParser;

/// <summary>
/// ?Œì´ë¸?ê´€??? í‹¸ë¦¬í‹° ë©”ì„œ?œë? ?œê³µ?˜ëŠ” ?¬í¼ ?´ë˜??/// </summary>
public static class TableHelper
{
    /// <summary>
    /// Excel ?Œí¬?œíŠ¸ë¡œë???CustomDataTable???ì„±?©ë‹ˆ??
    /// </summary>
    public static CustomDataTable GetTable(IXLWorksheet sheet)
    {
        var (tableName, columnTypes, data) = TableConverter.ConvertFromWorksheet(sheet);
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// Google Sheets ?°ì´?°ë¡œë¶€??CustomDataTable???ì„±?©ë‹ˆ??
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
    /// Dictionary ë¦¬ìŠ¤?¸ë¡œë¶€??CustomDataTable???ì„±?©ë‹ˆ??
    /// </summary>
    public static CustomDataTable GetTable(string tableName, Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data)
    {
        return new CustomDataTable(tableName, columnTypes, data);
    }

    /// <summary>
    /// CustomDataTable??JSON ë¬¸ì?´ë¡œ ë³€?˜í•©?ˆë‹¤.
    /// </summary>
    public static string ConvertDataTableToJSON(CustomDataTable table)
    {
        return table.ToJson();
    }
}

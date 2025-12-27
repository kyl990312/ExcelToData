using ClosedXML.Excel;
using Common.Utils;
using ExcelParser.Core;

namespace ExcelParser;

public static class TableHelper
{
    /// <summary>
    /// 주석이나 비어있는 행을 제외하고 유효한 행만 수집하여 반환합니다.
    /// </summary>
    public static List<IXLRow> GetRows(IXLWorksheet sheet, out int startIdx, out int endIdx)
    {
        var result = new List<IXLRow>();
        int idx = startIdx = endIdx = 0;
        foreach (var row in sheet.Rows())
        {
            idx++;
            var first = row.Cells().FirstOrDefault();
            if (first == null)
                continue;
            if (first.Value.ToString().ToLower().Equals("eof"))
            {
                endIdx = idx;
                break;
            }
            if (IsSkip(row))
                continue;
            result.Add(row);
        }

        startIdx = idx;
        return result;
    }

    public static bool IsSkip(IXLRow row)
    {
        if (row.CellCount() == 0)
            return true;
        var firstCell = row.Cells().FirstOrDefault();
        if (firstCell != null)
        {
            if (firstCell.Value.ToString().StartsWith(TableDefine.REGEX_START_EXCEPT_ROW))
                return true;
        }
        return false;
    }

    /// <summary>
    /// CustomDataTable을 생성하여 반환합니다.
    /// </summary>
    public static CustomDataTable GetTable(IXLWorksheet sheet)
    {
        return new CustomDataTable(sheet);
    }

    /// <summary>
    /// Google Sheets 데이터를 CustomDataTable로 변환합니다.
    /// </summary>
    public static CustomDataTable GetTable(string tableName, List<string[]> table)
    {
        if (table == null)
        {
            Logger.Instance.Error("[TableHelper] Input table is null");
            return null;
        }

        if (table.Count == 0)
        {
            Logger.Instance.Warning($"[TableHelper] Table is empty: {tableName}");
            return new CustomDataTable(new List<Dictionary<string, object>>(), tableName);
        }

        try
        {
            Logger.Instance.Log($"[TableHelper] Converting Google Sheets data to CustomDataTable: {tableName}");

            var columnNames = table[0];
            var columnTypes = table.Count > 1 ? table[1] : [];

            // 유효한 컬럼 정보 수집
            var validColumns = new Dictionary<int, (string name, EDataType dataType, string customTypeName, bool isArray)>();
            for (int i = 0; i < columnNames.Length; i++)
            {
                var columnName = columnNames[i];

                if (string.IsNullOrEmpty(columnName))
                    continue;

                if (columnName.StartsWith(TableDefine.REGEX_START_EXCEPT_COLUMN))
                    continue;

                var typeStr = i < columnTypes.Length ? columnTypes[i] : "String";
                var (dataType, customTypeName, isArray) = DataTypeHelper.ParseDataType(typeStr);

                validColumns[i] = (columnName, dataType, customTypeName, isArray);
            }

            if (validColumns.Count == 0)
            {
                Logger.Instance.Warning("[TableHelper] No valid columns found");
                return new CustomDataTable(new List<Dictionary<string, object>>(), tableName);
            }

            // 데이터 행 변환
            var dictList = new List<Dictionary<string, object>>();

            for (int rowIndex = 2; rowIndex < table.Count; rowIndex++)
            {
                var row = table[rowIndex];

                if (row.All(cell => string.IsNullOrWhiteSpace(cell)))
                    continue;

                if (row.Length > 0 && row[0]?.StartsWith(TableDefine.REGEX_START_EXCEPT_ROW) == true)
                    continue;

                if (row.Length > 0 && row[0]?.Equals("EOF", StringComparison.OrdinalIgnoreCase) == true)
                    break;

                var rowDict = new Dictionary<string, object>();

                foreach (var (colIndex, colInfo) in validColumns)
                {
                    var cellValue = colIndex < row.Length ? row[colIndex] : string.Empty;
                    var convertedValue = DataTypeHelper.ConvertValue(cellValue, colInfo.dataType, colInfo.customTypeName, colInfo.isArray);
                    rowDict[colInfo.name] = convertedValue;
                }

                dictList.Add(rowDict);
            }

            var customTable = new CustomDataTable(dictList, tableName);
            Logger.Instance.Log($"[TableHelper] CustomDataTable created: {customTable.Name} with {customTable.RowCount} rows");
            return customTable;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableHelper] Error converting table: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// CustomDataTable을 JSON 문자열로 변환합니다.
    /// </summary>
    public static string ConvertDataTableToJSON(CustomDataTable table)
    {
        return table.ToJson();
    }

    /// <summary>
    /// Dictionary 리스트를 CustomDataTable로 변환합니다.
    /// </summary>
    public static CustomDataTable ConvertToCustomDataTable(List<Dictionary<string, object>> data, string tableName = "DataTable")
    {
        if (data == null || data.Count == 0)
        {
            Logger.Instance.Warning("Empty data list provided to ConvertToCustomDataTable");
            return new CustomDataTable(new List<Dictionary<string, object>>(), tableName);
        }

        Logger.Instance.Log($"Converting {data.Count} rows to CustomDataTable: {tableName}");
        return new CustomDataTable(data, tableName);
    }

    /// <summary>
    /// 여러 Dictionary 리스트를 각각 CustomDataTable로 변환합니다.
    /// </summary>
    public static List<CustomDataTable> ConvertToCustomDataTableList(List<List<Dictionary<string, object>>> dataList, List<string> tableNames = null)
    {
        var result = new List<CustomDataTable>();

        for (int i = 0; i < dataList.Count; i++)
        {
            var tableName = tableNames != null && i < tableNames.Count ? tableNames[i] : $"DataTable{i}";
            var customTable = ConvertToCustomDataTable(dataList[i], tableName);
            result.Add(customTable);
        }

        Logger.Instance.Log($"Converted {dataList.Count} data lists to CustomDataTable list");
        return result;
    }

    /// <summary>
    /// Excel 셀 값을 지정된 타입으로 변환합니다.
    /// </summary>
    public static object GetConvertedCellValue(IXLCell cell, Type type)
    {
        return GetConvertedCellValue(cell, type, EDataType.String, string.Empty, false);
    }

    /// <summary>
    /// Excel 셀 값을 EDataType 기반으로 변환합니다.
    /// </summary>
    public static object GetConvertedCellValue(IXLCell cell, Type type, EDataType dataType, string customTypeName, bool isArray)
    {
        var cellText = cell.Value.IsText ? cell.Value.GetText() : cell.Value.ToString();

        // 사용자 정의 Enum인 경우 DataTypeHelper 사용
        if (dataType == EDataType.UserDefinedEnum)
        {
            return DataTypeHelper.ConvertValue(cellText, dataType, customTypeName, isArray);
        }

        // 기본 타입 변환
        if (!CheckValue(cell, type))
            return DataTypeHelper.GetDefaultValue(type);

        object result = null;

        if (type == typeof(DateTime))
        {
            result = Convert.ToDateTime(cell.Value);
        }
        else if (type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64) ||
                 type == typeof(Single) || type == typeof(Double))
        {
            result = Convert.ChangeType(cell.Value.GetNumber(), type);
        }
        else if (type == typeof(String))
        {
            if (cell.Value.IsNumber)
                result = cell.Value.GetNumber().ToString();
            else if (cell.Value.IsBoolean)
                result = cell.Value.GetBoolean().ToString();
            else if (cell.Value.IsDateTime)
                result = cell.Value.GetDateTime().ToString();
            else if (cell.Value.IsText)
                result = cell.Value.GetText();
            else
                result = cell.Value.ToString();
        }
        else if (type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType != null)
            {
                result = ConvertTextToArray(cellText, elementType);
            }
        }
        else if (type.IsEnum)
        {
            if (int.TryParse(cellText, out var iResult))
                result = Enum.ToObject(type, iResult);
            else if (Enum.TryParse(type, cellText, out var eResult))
                result = eResult;
        }

        return result ?? DataTypeHelper.GetDefaultValue(type);
    }

    private static object ConvertTextToArray(string text, Type elementType)
    {
        if (string.IsNullOrEmpty(text))
            return Array.CreateInstance(elementType, 0);

        var parts = text.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries);
        var array = Array.CreateInstance(elementType, parts.Length);

        for (int i = 0; i < parts.Length; i++)
        {
            try
            {
                array.SetValue(Convert.ChangeType(parts[i].Trim(), elementType), i);
            }
            catch
            {
                array.SetValue(elementType.IsValueType ? Activator.CreateInstance(elementType) : null, i);
            }
        }

        return array;
    }

    private static bool CheckValue(IXLCell cell, Type type)
    {
        try
        {
            if (type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64) ||
                type == typeof(Single) || type == typeof(Double))
            {
                if (!cell.Value.IsNumber)
                {
                    Logger.Instance.Warning($"Cell at {cell.Address} is not a number but trying to convert to {type}");
                    return false;
                }

                double number = cell.Value.GetNumber();

                if (type == typeof(Int16) && (number < Int16.MinValue || number > Int16.MaxValue))
                    return false;
                if (type == typeof(Int32) && (number < Int32.MinValue || number > Int32.MaxValue))
                    return false;
                if (type == typeof(Int64) && (number < Int64.MinValue || number > Int64.MaxValue))
                    return false;

                return true;
            }

            if (type == typeof(String))
                return true;

            if (type.IsArray)
                return !string.IsNullOrEmpty(cell.Value.ToString());

            if (type.IsEnum)
            {
                string text = cell.Value.ToString();
                return int.TryParse(text, out _) || Enum.GetNames(type).Contains(text);
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Exception at {cell.Address} for type {type}: {ex.Message}");
            return false;
        }
    }
}

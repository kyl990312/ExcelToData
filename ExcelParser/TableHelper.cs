using ClosedXML.Excel;
using System.Data;
using System.Text.Json;
using Common.Utils;

namespace ExcelParser;

public static class TableHelper
{
    /// <summary>
    /// 주석이나 비어있는 행을 제외하고 유효한 행만 수집하여 반환합니다.
    /// </summary>
    public static List<IXLRow> GetRows(IXLWorksheet sheet, out int startIdx, out int endIdx)
    {
        var result = new List<IXLRow>();
        int idx = startIdx = endIdx= 0;
        foreach (var row in sheet.Rows())
        {
            idx++;
            var first = row.Cells().FirstOrDefault();
            if(first == null)
                continue;
            if (first.Value.ToString().ToLower().Equals("eof"))
            {
                endIdx = idx;
                break;
            }
            if(IsSkip(row))
                continue;
            result.Add(row);
        }

        startIdx = idx;
        return result;
    }
    
    public static bool IsSkip(IXLRow row)
    {
        if(row.CellCount() == 0)
            return true;
        // 주석 제외
        var firstCell = row.Cells().FirstOrDefault();
        if (firstCell != null)
        {
            if(firstCell.Value.ToString().StartsWith(TableDefine.REGEX_START_EXCEPT_ROW))
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
    /// CustomDataTable을 JSON 문자열로 변환합니다.
    /// </summary>
    public static string ConvertDataTableToJSON(CustomDataTable table)
    {
        return table.ToJson();
    }

    /// <summary>
    /// Dictionary 리스트를 CustomDataTable로 변환합니다.
    /// </summary>
    /// <param name="data">변환할 Dictionary 리스트</param>
    /// <param name="tableName">테이블 이름 (선택사항)</param>
    /// <returns>변환된 CustomDataTable</returns>
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
    /// <param name="dataList">변환할 Dictionary 리스트들</param>
    /// <param name="tableNames">각 테이블의 이름들 (선택사항)</param>
    /// <returns>변환된 CustomDataTable 리스트</returns>
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

    public static object GetConvertedCellValue(IXLCell cell, Type type)
    {
        if (CheckValue(cell, type) == false)
            return GetDefaultValue(type);

        object result = 0;
        if(type == typeof(DateTime))
        {
            result = Convert.ToDateTime(cell.Value);   
        }
        else if (type == typeof(Int16) ||
            type == typeof(Int32) ||
            type == typeof(Int64) ||
            type == typeof(Single) ||
            type == typeof(Double))
        {
            result = Convert.ChangeType(cell.Value.GetNumber(), type);
        }
        else if (type == typeof(String))
        {
            if(cell.Value.IsNumber)
                result = Convert.ChangeType(cell.Value.GetNumber(), type);
            if(cell.Value.IsBoolean)
                result = Convert.ChangeType(cell.Value.GetBoolean(), type);
            if(cell.Value.IsDateTime)
                result = Convert.ChangeType(cell.Value.GetDateTime(), type);
            if(cell.Value.IsText)
                result = Convert.ChangeType(cell.Value.GetText(), type);
        }
        else if (type.IsArray)
        {
            var elementType = Type.GetType(type.ToString().Replace("[]", ""));
            if (elementType == null)
            {
                Logger.Instance.Warning(
                    $"Parsing Failed(Try to parse Array, but failed)(type : {type.ToString()}, value: {cell.Value.GetText()})");
            }
            else
            {
                result = ConvertTextToArray(cell.Value.GetText(), elementType);
            }
        }
        else if (type.IsEnum)
        {
            var value = cell.Value.GetText();
            if (int.TryParse(value, out var iResult))
                result = Convert.ChangeType(iResult, type);
            else if (Enum.TryParse(type, value, out var eResult))
                result = eResult;
            else
            {
                Logger.Instance.Warning(
                    $"Parsing Failed(Try to parse Enum, but failed)(type : {type.ToString()}, value: {value})");
            }
        }
        else
        {
            Logger.Instance.Warning(
                $"Parsing Failed(Cannot find type)(type : {type.ToString()}, value: {cell.Value.ToString()})");
        }

        return result;
    }

    private static object ConvertTextToArray(string text, Type type)
    {
        var parts = text.Trim('[', ']').Replace(" ", "").Split(",");
        var array = Array.CreateInstance(type, parts.Length);
        for (int i = 0; i < array.Length; i++)
        {
            array.SetValue(Convert.ChangeType(parts[i], type), i);
        }

        return array;
    }

    private static bool CheckValue(IXLCell cell, Type type)
    {
        try
        {
            if (type == typeof(Int16) || type == typeof(Int32) ||
                type == typeof(Int64) || type == typeof(Single) ||
                type == typeof(Double))
            {
                if (!cell.Value.IsNumber)
                {
                    Logger.Instance.Warning($"Cell at {cell.Address} is not a number but trying to convert to {type}");
                    return false;
                }

                double number = cell.Value.GetNumber();

                if (type == typeof(Int16) && (number < Int16.MinValue || number > Int16.MaxValue))
                {
                    Logger.Instance.Warning($"Value {number} at {cell.Address} is outside the range of Int16");
                    return false;
                }
                else if (type == typeof(Int32) && (number < Int32.MinValue || number > Int32.MaxValue))
                {
                    Logger.Instance.Warning($"Value {number} at {cell.Address} is outside the range of Int32");
                    return false;
                }
                else if (type == typeof(Int64) && (number < Int64.MinValue || number > Int64.MaxValue))
                {
                    Logger.Instance.Warning($"Value {number} at {cell.Address} is outside the range of Int64");
                    return false;
                }
                else if (type == typeof(Single) && (number < Single.MinValue || number > Single.MaxValue))
                {
                    Logger.Instance.Warning($"Value {number} at {cell.Address} is outside the range of Single");
                    return false;
                }
                else if (type == typeof(Double) && (double.IsInfinity(number) || double.IsNaN(number)))
                {
                    Logger.Instance.Warning(
                        $"Value {number} at {cell.Address} is not a valid Double (Infinity or NaN)");
                    return false;
                }

                return true;
            }
            else if (type == typeof(String))
            {
                return true; // 문자열은 항상 변환 가능
            }
            else if (type.IsArray)
            {
                string text = cell.Value.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    Logger.Instance.Warning(
                        $"Cell at {cell.Address} is empty or null but trying to convert to array type {type}");
                    return false;
                }

                return true;
            }
            else if (type.IsEnum)
            {
                string text = cell.Value.ToString();
                bool isValid = int.TryParse(text, out _) || Enum.GetNames(type).Contains(text);

                if (!isValid)
                {
                    Logger.Instance.Warning($"Value '{text}' at {cell.Address} cannot be parsed as enum type {type}");
                }

                return isValid;
            }

            Logger.Instance.Warning($"Unsupported type {type} for cell at {cell.Address}");
            return false; // 지원하지 않는 타입
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning(
                $"Exception occurred while checking value at {cell.Address} for type {type}: {ex.Message}");
            return false; // 예외 발생 시 유효하지 않음
        }
    }

    private static object GetDefaultValue(Type type)
    {
        if (type == typeof(Int16))
            return (Int16)0;
        if (type == typeof(Int32))
            return (Int32)0;
        if (type == typeof(Int64))
            return (Int64)0;
        if (type == typeof(Single))
            return (Single)0.0f;
        if (type == typeof(Double))
            return (Double)0.0;
        if (type == typeof(String))
            return string.Empty;
        if (type.IsArray)
        {
            // 배열 타입의 기본값은 빈 배열
            Type elementType = type.GetElementType();
            return Array.CreateInstance(elementType, 0);
        }

        if (type.IsEnum)
        {
            // Enum의 기본값은 첫 번째 정의된 값 또는 0
            Array values = Enum.GetValues(type);
            return values.Length > 0 ? values.GetValue(0) : Enum.ToObject(type, 0);
        }

        // 그 외 타입은 기본값
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

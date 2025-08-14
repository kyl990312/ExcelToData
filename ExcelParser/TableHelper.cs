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
    
    public static DataTable GetTable(IXLWorksheet sheet)
    {
        var table = new DataTable();
        
        var header = new TableHeader(sheet);
        foreach (var column in header.Headers)
        {
            table.Columns.Add(column.name, column.type);
        }

        var rows = GetRows(sheet, out _, out _);
        for (int i = 0; i < header.HeaderRowCount; i++)
        {
            if (rows.Count > 0)
                rows.RemoveAt(0);
        }
        foreach (var row in rows)
        {
            var columnIndices = header.ColumnIndices;
            var values = new object[header.ColumnCount];
            int idx = 0;
            foreach (var columnIndex in columnIndices)
            {
                var column = header[columnIndex];
                values[idx++] = GetConvertedCellValue(row.Cell(columnIndex+1), column.type);
            }
            table.Rows.Add(values);
        }

        return table;
    }

    public static string ConverDataTableToJSON(DataTable table)
    {
        List<Dictionary<string, object>> dictTable = new List<Dictionary<string, object>>();
        foreach (DataRow row in table.Rows)
        {
            var rowDict = new Dictionary<string, object>();
            foreach (DataColumn column in table.Columns)
            {
                rowDict.Add(column.ColumnName, row[column]);
            }
            dictTable.Add(rowDict);
        }

        return JsonSerializer.Serialize(dictTable);
    }

    /// <summary>
    /// DataTable을 Dictionary 형태로 변환합니다
    /// </summary>
    /// <param name="table">변환할 DataTable</param>
    /// <returns>Dictionary 형태의 데이터</returns>
    public static List<Dictionary<string, object>> ConvertDataTableToBinary(DataTable table)
    {
        var result = new List<Dictionary<string, object>>();
        
        foreach (DataRow row in table.Rows)
        {
            var rowDict = new Dictionary<string, object>();
            foreach (DataColumn column in table.Columns)
            {
                var value = row[column];
                rowDict.Add(column.ColumnName, value == DBNull.Value ? null : value);
            }
            result.Add(rowDict);
        }
        
        return result;
    }

    /// <summary>
    /// Dictionary 형태의 데이터를 DataTable로 변환합니다
    /// </summary>
    /// <param name="data">변환할 데이터</param>
    /// <param name="tableName">테이블 이름</param>
    /// <returns>변환된 DataTable</returns>
    public static DataTable ConvertBinaryToDataTable(List<Dictionary<string, object>> data, string tableName = "Data")
    {
        var table = new DataTable(tableName);
        
        if (data == null || data.Count == 0)
        {
            return table;
        }
        
        // 칼럼 정보 추출 (모든 행에서 칼럼명 수집)
        var allColumns = new HashSet<string>();
        foreach (var row in data)
        {
            foreach (var key in row.Keys)
            {
                allColumns.Add(key);
            }
        }
        
        // 칼럼 타입 추론 및 테이블 스키마 생성
        foreach (var columnName in allColumns)
        {
            var columnType = InferColumnType(data, columnName);
            table.Columns.Add(columnName, columnType);
        }
        
        // 데이터 행 추가
        foreach (var rowData in data)
        {
            var row = table.NewRow();
            foreach (var column in table.Columns.Cast<DataColumn>())
            {
                if (rowData.ContainsKey(column.ColumnName))
                {
                    var value = rowData[column.ColumnName];
                    row[column.ColumnName] = value ?? DBNull.Value;
                }
                else
                {
                    row[column.ColumnName] = DBNull.Value;
                }
            }
            table.Rows.Add(row);
        }
        
        return table;
    }

    public static object GetConvertedCellValue(IXLCell cell, Type type)
    {
        if (CheckValue(cell, type) == false)
            return GetDefaultValue(type);

        object result = 0;
        try
        {
            if (type == typeof(Int16) ||
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
                    result = Convert.ChangeType(cell.Value.GetNumber().ToString(), type);
                else if(cell.Value.IsDateTime)
                    result = Convert.ChangeType(cell.Value.GetDateTime().ToString(), type);
                else if(cell.Value.IsBoolean)
                    result = Convert.ChangeType(cell.Value.GetBoolean().ToString(), type);
                else if(cell.Value.IsTimeSpan)
                    result = Convert.ChangeType(cell.Value.GetTimeSpan().ToString(), type);
                else
                    result = Convert.ChangeType(cell.Value.GetText(), type);
            }
            else if (type.IsArray)
            {
                var elementType = Type.GetType(type.ToString().Replace("[]", ""));
                if (elementType == null)
                {
                    Logger.Instance.Warning(
                        $"Parsing Failed(Try to parse Enum, but failed)(type : {type.ToString()}, value: {cell.Value.GetText()})");
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
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"[TableHelper] GetConvertedCellValue failed({type.ToString()}, value: {cell.Value
                .ToString()}) - {e.Message}\n{e.StackTrace}");
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
        try
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
        catch (Exception e)
        {
            Logger.Instance.Error($"[TableHelper] - GetDefaultValue ({e.Message})\n{e.StackTrace}");
            return null;
        }
    }
    
    private static Type InferColumnType(List<Dictionary<string, object>> data, string columnName)
    {
        Type resultType = typeof(string); // 기본값
        
        foreach (var row in data)
        {
            if (row.ContainsKey(columnName) && row[columnName] != null)
            {
                var valueType = row[columnName].GetType();
                
                // 첫 번째 null이 아닌 값의 타입을 사용
                if (resultType == typeof(string))
                {
                    resultType = valueType;
                }
                else if (resultType != valueType)
                {
                    // 타입이 다르면 더 일반적인 타입으로 변경
                    if (IsNumericType(resultType) && IsNumericType(valueType))
                    {
                        resultType = GetWiderNumericType(resultType, valueType);
                    }
                    else
                    {
                        resultType = typeof(string); // 가장 일반적인 타입
                    }
                }
            }
        }
        
        return resultType;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64) ||
               type == typeof(Single) || type == typeof(Double) || type == typeof(decimal);
    }

    private static Type GetWiderNumericType(Type type1, Type type2)
    {
        var numericHierarchy = new Type[]
        {
            typeof(Int16), typeof(Int32), typeof(Int64),
            typeof(Single), typeof(Double), typeof(decimal)
        };
        
        int index1 = Array.IndexOf(numericHierarchy, type1);
        int index2 = Array.IndexOf(numericHierarchy, type2);
        
        if (index1 == -1) index1 = 0;
        if (index2 == -1) index2 = 0;
        
        return numericHierarchy[Math.Max(index1, index2)];
    }

    private static bool AreDataTableValuesEqual(object value1, object value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;
        
        if (value1 is Array array1 && value2 is Array array2)
        {
            if (array1.Length != array2.Length) return false;
            for (int i = 0; i < array1.Length; i++)
            {
                if (!Equals(array1.GetValue(i), array2.GetValue(i)))
                    return false;
            }
            return true;
        }
        
        return Equals(value1, value2);
    }
}

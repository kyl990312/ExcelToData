using ClosedXML.Excel;
using System.Data;
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
    /// DataTable을 CustomDataTable로 변환합니다.
    /// Google Sheets에서 읽어온 데이터를 Excel과 동일한 방식으로 처리합니다.
    /// 1행: 컬럼명, 2행부터: 데이터 (Google Sheets용)
    /// </summary>
    /// <param name="table">변환할 DataTable</param>
    /// <returns>변환된 CustomDataTable</returns>
    public static CustomDataTable GetTable(DataTable table)
    {
        if (table == null)
        {
            Logger.Instance.Error("[TableHelper] Input DataTable is null");
            return null;
        }

        if (table.Rows.Count == 0)
        {
            Logger.Instance.Warning($"[TableHelper] DataTable is empty: {table.TableName}");
            return new CustomDataTable(new List<Dictionary<string, object>>(), table.TableName);
        }

        try
        {
            Logger.Instance.Log($"[TableHelper] Converting Google Sheets DataTable to CustomDataTable: {table.TableName}");
            
            // Google Sheets DataTable의 컬럼명을 그대로 사용하고 모든 데이터를 string 타입으로 처리
            var columnTypes = new Dictionary<string, Type>();
            
            // DataTable의 컬럼명과 타입 정보 추출
            foreach (DataColumn column in table.Columns)
            {
                var columnName = column.ColumnName;
                
                // 제외 컬럼 필터링 (TableDefine 상수 사용)
                if (columnName.StartsWith(TableDefine.REGEX_START_EXCEPT_COLUMN))
                {
                    Logger.Instance.Log($"[TableHelper] Skipping column: {columnName} (excluded by prefix)");
                    continue;
                }
                
                if (string.IsNullOrEmpty(columnName))
                {
                    Logger.Instance.Error($"[TableHelper] Column name is empty at index: {column.Ordinal}");
                    continue;
                }
                
                // Google Sheets에서는 모든 데이터가 string으로 처리됨
                columnTypes[columnName] = typeof(string);
                Logger.Instance.Log($"[TableHelper] Added column: {columnName} (String)");
            }
            
            if (columnTypes.Count == 0)
            {
                Logger.Instance.Warning("[TableHelper] No valid columns found");
                return new CustomDataTable(new List<Dictionary<string, object>>(), table.TableName);
            }
            
            // 데이터 행들을 Dictionary 리스트로 변환
            var dictList = new List<Dictionary<string, object>>();
            
            foreach (DataRow row in table.Rows)
            {
                // 빈 행 체크
                if (row.ItemArray.All(field => string.IsNullOrWhiteSpace(field?.ToString())))
                {
                    continue;
                }
                
                // 제외 행 필터링 (TableDefine 상수 사용)
                if (row.ItemArray.Length > 0 && 
                    row.ItemArray[0]?.ToString().StartsWith(TableDefine.REGEX_START_EXCEPT_ROW) == true)
                {
                    Logger.Instance.Log($"[TableHelper] Skipping row (comment row)");
                    continue;
                }

                // EOF 마커 확인
                if (row.ItemArray.Length > 0 && 
                    row.ItemArray[0]?.ToString().Equals("EOF", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Logger.Instance.Log($"[TableHelper] Reached EOF marker");
                    break;
                }

                var rowDict = new Dictionary<string, object>();
                
                // 유효한 컬럼들에 대해서만 데이터 추출
                foreach (var columnName in columnTypes.Keys)
                {
                    if (table.Columns.Contains(columnName))
                    {
                        var cellValue = row[columnName]?.ToString() ?? string.Empty;
                        rowDict[columnName] = cellValue;
                    }
                    else
                    {
                        rowDict[columnName] = string.Empty;
                    }
                }
                
                dictList.Add(rowDict);
            }
            
            Logger.Instance.Log($"[TableHelper] Extracted {dictList.Count} valid data rows");
            
            // CustomDataTable 생성 (Dictionary 리스트 생성자 사용)
            var customTable = new CustomDataTable(dictList, table.TableName);
            
            Logger.Instance.Log($"[TableHelper] CustomDataTable created: {customTable.Name} with {customTable.RowCount} rows and {customTable.ColumnCount} columns");
            return customTable;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableHelper] Error converting DataTable to CustomDataTable: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 문자열을 지정된 타입으로 변환합니다. (TableHelper.GetConvertedCellValue의 문자열 버전)
    /// </summary>
    private static object ConvertStringToType(string value, Type type, string columnName, int rowNumber)
    {
        if (string.IsNullOrEmpty(value))
        {
            return GetDefaultValue(type);
        }
        
        try
        {
            if (type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64) || 
                type == typeof(Single) || type == typeof(Double))
            {
                if (!double.TryParse(value, out double number))
                {
                    Logger.Instance.Warning($"[TableHelper] Value '{value}' is not a number for column '{columnName}' at row {rowNumber}");
                    return GetDefaultValue(type);
                }

                // 범위 체크 및 변환
                if (type == typeof(Int16) && (number < Int16.MinValue || number > Int16.MaxValue))
                {
                    Logger.Instance.Warning($"[TableHelper] Value {number} is outside the range of Int16 for column '{columnName}' at row {rowNumber}");
                    return GetDefaultValue(type);
                }
                else if (type == typeof(Int32) && (number < Int32.MinValue || number > Int32.MaxValue))
                {
                    Logger.Instance.Warning($"[TableHelper] Value {number} is outside the range of Int32 for column '{columnName}' at row {rowNumber}");
                    return GetDefaultValue(type);
                }
                
                return Convert.ChangeType(number, type);
            }
            else if (type == typeof(String))
            {
                return value;
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType == null)
                {
                    Logger.Instance.Warning($"[TableHelper] Cannot determine array element type for column '{columnName}' at row {rowNumber}");
                    return GetDefaultValue(type);
                }
                
                return ConvertTextToArray(value, elementType);
            }
            else if (type.IsEnum)
            {
                if (int.TryParse(value, out var iResult))
                    return Convert.ChangeType(iResult, type);
                else if (Enum.TryParse(type, value, out var eResult))
                    return eResult;
                else
                {
                    Logger.Instance.Warning($"[TableHelper] Cannot parse '{value}' as enum type {type} for column '{columnName}' at row {rowNumber}");
                    return GetDefaultValue(type);
                }
            }
            else
            {
                Logger.Instance.Warning($"[TableHelper] Unsupported type {type} for column '{columnName}' at row {rowNumber}");
                return GetDefaultValue(type);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"[TableHelper] Error converting '{value}' to {type.Name} for column '{columnName}' at row {rowNumber}: {ex.Message}");
            return GetDefaultValue(type);
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

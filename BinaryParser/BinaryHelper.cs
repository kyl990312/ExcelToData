using System.Text.Json;
using Common.Utils;

namespace BinaryParser;

/// <summary>
/// Binary 작업을 위한 편의 기능을 제공하는 헬퍼 클래스
/// Dictionary 형태의 데이터만 처리합니다
/// </summary>
public static class BinaryHelper
{
    private static readonly BinaryReader _reader = new();
    private static readonly BinaryWriter _writer = new();

    /// <summary>
    /// 구조화된 바이너리 파일을 Dictionary 형태로 로드합니다
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>로드된 데이터 (List&lt;Dictionary&lt;string, object&gt;&gt; 형태)</returns>
    public static List<Dictionary<string, object>> LoadFromFile(string filePath)
    {
        return _reader.ReadFromFile(filePath);
    }

    /// <summary>
    /// Dictionary 형태의 데이터를 구조화된 바이너리 파일로 저장합니다
    /// </summary>
    /// <param name="data">저장할 데이터</param>
    /// <param name="filePath">저장할 파일 경로</param>
    public static void SaveToFile(List<Dictionary<string, object>> data, string filePath)
    {
        _writer.WriteToFile(data, filePath);
    }

    /// <summary>
    /// Dictionary 형태의 데이터를 바이트 배열로 변환합니다
    /// </summary>
    /// <param name="data">변환할 데이터</param>
    /// <returns>바이너리 데이터</returns>
    public static byte[] ConvertToBytes(List<Dictionary<string, object>> data)
    {
        return _writer.WriteToBytes(data);
    }

    /// <summary>
    /// 바이트 배열을 Dictionary 형태의 데이터로 변환합니다
    /// </summary>
    /// <param name="binaryData">바이너리 데이터</param>
    /// <returns>변환된 데이터</returns>
    public static List<Dictionary<string, object>> ConvertFromBytes(byte[] binaryData)
    {
        return _reader.ReadFromBytes(binaryData);
    }

    /// <summary>
    /// 구조화된 바이너리 파일을 JSON으로 변환합니다
    /// </summary>
    /// <param name="binaryFilePath">바이너리 파일 경로</param>
    /// <param name="jsonFilePath">JSON 파일 경로</param>
    public static void ConvertToJson(string binaryFilePath, string jsonFilePath)
    {
        try
        {
            var data = LoadFromFile(binaryFilePath);
            var json = ConvertToJSON(data);
            
            var directory = Path.GetDirectoryName(jsonFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(jsonFilePath, json);
            Logger.Instance.Log($"[BinaryHelper] Converted to JSON: {binaryFilePath} -> {jsonFilePath}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[BinaryHelper] Error converting to JSON: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// JSON 파일을 구조화된 바이너리로 변환합니다
    /// </summary>
    /// <param name="jsonFilePath">JSON 파일 경로</param>
    /// <param name="binaryFilePath">바이너리 파일 경로</param>
    public static void ConvertFromJson(string jsonFilePath, string binaryFilePath)
    {
        try
        {
            var jsonText = File.ReadAllText(jsonFilePath);
            var data = ConvertFromJSON(jsonText);
            SaveToFile(data, binaryFilePath);
            
            Logger.Instance.Log($"[BinaryHelper] Converted from JSON: {jsonFilePath} -> {binaryFilePath}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[BinaryHelper] Error converting from JSON: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 구조화된 바이너리 파일의 정보를 출력합니다
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>파일 정보 문자열</returns>
    public static string GetFileInfo(string filePath)
    {
        try
        {
            var data = LoadFromFile(filePath);
            var info = new System.Text.StringBuilder();
            
            info.AppendLine($"File: {filePath}");
            info.AppendLine($"Rows: {data.Count}");
            
            if (data.Count > 0)
            {
                var firstRow = data[0];
                info.AppendLine($"Columns: {firstRow.Count}");
                info.AppendLine("Column Details:");
                
                foreach (var kvp in firstRow)
                {
                    var type = kvp.Value?.GetType().Name ?? "null";
                    info.AppendLine($"  - {kvp.Key}: {type}");
                }
                
                // 샘플 데이터 표시
                if (data.Count > 0)
                {
                    info.AppendLine("\nSample Data (first row):");
                    foreach (var kvp in firstRow)
                    {
                        var valueStr = kvp.Value?.ToString() ?? "null";
                        if (valueStr.Length > 50)
                            valueStr = valueStr.Substring(0, 47) + "...";
                        info.AppendLine($"  {kvp.Key}: {valueStr}");
                    }
                }
            }
            
            return info.ToString();
        }
        catch (Exception ex)
        {
            return $"Error reading file info: {ex.Message}";
        }
    }

    /// <summary>
    /// Dictionary 형태의 데이터를 JSON 문자열로 변환합니다
    /// </summary>
    /// <param name="data">변환할 데이터</param>
    /// <returns>JSON 문자열</returns>
    public static string ConvertToJSON(List<Dictionary<string, object>> data)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    /// <summary>
    /// JSON 문자열을 Dictionary 형태의 데이터로 변환합니다
    /// </summary>
    /// <param name="jsonText">JSON 문자열</param>
    /// <returns>변환된 데이터</returns>
    public static List<Dictionary<string, object>> ConvertFromJSON(string jsonText)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            
            if (root.ValueKind != JsonValueKind.Array)
            {
                Logger.Instance.Warning("[BinaryHelper] JSON root is not an array");
                return new List<Dictionary<string, object>>();
            }
            
            var result = new List<Dictionary<string, object>>();
            
            foreach (var element in root.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var row = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        row[property.Name] = ConvertJsonElementToValue(property.Value);
                    }
                    result.Add(row);
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[BinaryHelper] Error converting JSON to data: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 두 데이터 세트를 비교하여 동일한지 확인합니다
    /// </summary>
    /// <param name="data1">첫 번째 데이터</param>
    /// <param name="data2">두 번째 데이터</param>
    /// <returns>동일 여부</returns>
    public static bool AreDataEqual(List<Dictionary<string, object>> data1, List<Dictionary<string, object>> data2)
    {
        if (data1.Count != data2.Count)
            return false;

        for (int i = 0; i < data1.Count; i++)
        {
            var row1 = data1[i];
            var row2 = data2[i];

            if (row1.Count != row2.Count)
                return false;

            foreach (var kvp in row1)
            {
                if (!row2.ContainsKey(kvp.Key))
                    return false;

                if (!AreValuesEqual(kvp.Value, row2[kvp.Key]))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 데이터의 무결성을 검증합니다
    /// </summary>
    /// <param name="data">검증할 데이터</param>
    /// <returns>검증 결과</returns>
    public static ValidationResult ValidateData(List<Dictionary<string, object>> data)
    {
        var result = new ValidationResult();

        if (data == null)
        {
            result.IsValid = false;
            result.ErrorMessage = "Data is null";
            return result;
        }

        if (data.Count == 0)
        {
            result.IsValid = true;
            result.Message = "Empty data set";
            return result;
        }

        // 칼럼 일관성 검사
        var firstRowColumns = data[0].Keys.ToHashSet();
        for (int i = 1; i < data.Count; i++)
        {
            var currentRowColumns = data[i].Keys.ToHashSet();
            if (!firstRowColumns.SetEquals(currentRowColumns))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Column mismatch at row {i}";
                return result;
            }
        }

        result.IsValid = true;
        result.Message = $"Data validation passed. {data.Count} rows, {firstRowColumns.Count} columns.";
        return result;
    }

    private static object ConvertJsonElementToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => ConvertJsonArrayToArray(element),
            JsonValueKind.Object => element.GetRawText(), // 객체는 JSON 문자열로 저장
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static object ConvertJsonArrayToArray(JsonElement arrayElement)
    {
        var list = new List<object>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            list.Add(ConvertJsonElementToValue(item));
        }
        return list.ToArray();
    }

    private static bool AreValuesEqual(object value1, object value2)
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

    /// <summary>
    /// 데이터 검증 결과
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}

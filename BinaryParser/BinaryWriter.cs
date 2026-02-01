using System.Text;
#if !UNITY_BUILD
using TableAutomation.Common.Utils;
#endif

namespace BinaryParser;

/// <summary>
/// Dictionary 형태의 데이터를 구조화된 바이너리 형식으로 저장하는 클래스
/// </summary>
public class BinaryWriter
{
    /// <summary>
    /// Dictionary 형태의 데이터를 바이너리 파일로 저장합니다
    /// </summary>
    /// <param name="columnTypes">칼럼명과 타입 문자열 딕셔너리</param>
    /// <param name="data">저장할 데이터</param>
    /// <param name="filePath">저장할 파일 경로</param>
    public void WriteToFile(Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data, string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new System.IO.BinaryWriter(fileStream, Encoding.UTF8);

            WriteDataToBinary(columnTypes, data, writer);
            
#if !UNITY_BUILD
            Logger.Instance.Log($"[BinaryWriter] Successfully wrote binary file: {filePath}");
#endif
        }
        catch (Exception ex)
        {
#if !UNITY_BUILD
            Logger.Instance.Error($"[BinaryWriter] Error writing binary file {filePath}: {ex.Message}");
#endif
            throw;
        }
    }

    /// <summary>
    /// Dictionary 형태의 데이터를 바이트 배열로 변환합니다
    /// </summary>
    /// <param name="columnTypes">칼럼명과 타입 문자열 딕셔너리</param>
    /// <param name="data">변환할 데이터</param>
    /// <returns>바이너리 데이터</returns>
    public byte[] WriteToBytes(Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new System.IO.BinaryWriter(memoryStream, Encoding.UTF8);

        WriteDataToBinary(columnTypes, data, writer);
        return memoryStream.ToArray();
    }

    private void WriteDataToBinary(Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data, System.IO.BinaryWriter writer)
    {
        if (columnTypes == null || columnTypes.Count == 0)
        {
            writer.Write(0); // 칼럼 갯수
            writer.Write(0); // 데이터 갯수
            return;
        }

        // Header 정보 작성
        WriteHeader(columnTypes, data?.Count ?? 0, writer);
        
        // Data 정보 작성 (칼럼별로 저장)
        if (data != null && data.Count > 0)
        {
            WriteDataByColumns(columnTypes, data, writer);
        }
    }

    /// <summary>
    /// 타입 문자열을 정규화합니다 (System. 접두어 제거)
    /// </summary>
    private string NormalizeTypeString(string typeStr)
    {
        if (string.IsNullOrEmpty(typeStr))
            return "String";

        if (typeStr.StartsWith("System."))
            return typeStr.Substring(7);

        return typeStr;
    }

    private void WriteHeader(Dictionary<string, string> columnTypes, int rowCount, System.IO.BinaryWriter writer)
    {
        // 1. 칼럼 갯수
        writer.Write(columnTypes.Count);
        
        // 2. 데이터 갯수 (행 갯수)
        writer.Write(rowCount);
        
        // 3. 각 칼럼의 메타데이터
        foreach (var kvp in columnTypes)
        {
            // 칼럼 이름
            writer.Write(kvp.Key);
            
            // 칼럼 타입 (System. 접두어 제거)
            writer.Write(NormalizeTypeString(kvp.Value));
        }
    }

    private void WriteDataByColumns(Dictionary<string, string> columnTypes, List<Dictionary<string, object>> data, System.IO.BinaryWriter writer)
    {
        foreach (var kvp in columnTypes)
        {
            var columnName = kvp.Key;
            var typeStr = NormalizeTypeString(kvp.Value);
            WriteColumnData(data, columnName, typeStr, writer);
        }
    }

    private void WriteColumnData(List<Dictionary<string, object>> data, string columnName, string typeStr, System.IO.BinaryWriter writer)
    {
        foreach (var row in data)
        {
            object value = row.TryGetValue(columnName, out var v) ? v : null;
            
            if (value == null)
            {
                writer.Write(true); // null 플래그
                continue;
            }
            
            writer.Write(false); // null이 아님
            WriteValueByTypeString(value, typeStr, writer);
        }
    }

    private void WriteValueByTypeString(object value, string typeStr, System.IO.BinaryWriter writer)
    {
        try
        {
            // 배열 타입 처리
            if (typeStr.EndsWith("[]"))
            {
                var elementTypeStr = typeStr.Substring(0, typeStr.Length - 2);
                WriteArrayValue(value, elementTypeStr, writer);
                return;
            }

            switch (typeStr)
            {
                case "Int16":
                    writer.Write(Convert.ToInt16(value));
                    break;
                case "Int32":
                    writer.Write(Convert.ToInt32(value));
                    break;
                case "Int64":
                    writer.Write(Convert.ToInt64(value));
                    break;
                case "Single":
                    writer.Write(Convert.ToSingle(value));
                    break;
                case "Double":
                    writer.Write(Convert.ToDouble(value));
                    break;
                case "Boolean":
                    writer.Write(Convert.ToBoolean(value));
                    break;
                case "String":
                    writer.Write(value.ToString() ?? string.Empty);
                    break;
                case "DateTime":
                    writer.Write(Convert.ToDateTime(value).Ticks);
                    break;
                default:
                    // UserEnum 또는 기타 타입은 문자열로 저장
                    writer.Write(value.ToString() ?? string.Empty);
                    break;
            }
        }
        catch (Exception ex)
        {
#if !UNITY_BUILD
            Logger.Instance.Warning($"[BinaryWriter] Error writing value '{value}' as type '{typeStr}': {ex.Message}");
#endif
            writer.Write(value?.ToString() ?? string.Empty);
        }
    }

    private void WriteArrayValue(object value, string elementTypeStr, System.IO.BinaryWriter writer)
    {
        if (value is Array array)
        {
            writer.Write(array.Length);
            
            foreach (var element in array)
            {
                if (element == null)
                {
                    writer.Write(true);
                    continue;
                }
                
                writer.Write(false);
                WriteValueByTypeString(element, elementTypeStr, writer);
            }
        }
        else if (value is System.Collections.IEnumerable enumerable)
        {
            var list = new List<object>();
            foreach (var item in enumerable)
            {
                list.Add(item);
            }
            
            writer.Write(list.Count);
            
            foreach (var item in list)
            {
                if (item == null)
                {
                    writer.Write(true);
                    continue;
                }
                
                writer.Write(false);
                WriteValueByTypeString(item, elementTypeStr, writer);
            }
        }
        else
        {
            writer.Write(0);
        }
    }
}

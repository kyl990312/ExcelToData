using System.Text;
using Common.Utils;

namespace BinaryParser;

/// <summary>
/// Dictionary 형태의 데이터를 구조화된 바이너리 형식으로 저장하는 클래스
/// </summary>
public class BinaryWriter
{
    /// <summary>
    /// Dictionary 형태의 데이터를 바이너리 파일로 저장합니다
    /// </summary>
    /// <param name="data">저장할 데이터 (List&lt;Dictionary&lt;string, object&gt;&gt; 형태)</param>
    /// <param name="filePath">저장할 파일 경로</param>
    public void WriteToFile(List<Dictionary<string, object>> data, string filePath)
    {
        try
        {
            // 디렉토리 생성
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new System.IO.BinaryWriter(fileStream, Encoding.UTF8);

            WriteDataToBinary(data, writer);
            
            Logger.Instance.Log($"[BinaryWriter] Successfully wrote binary file: {filePath}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[BinaryWriter] Error writing binary file {filePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Dictionary 형태의 데이터를 바이트 배열로 변환합니다
    /// </summary>
    /// <param name="data">변환할 데이터</param>
    /// <returns>바이너리 데이터</returns>
    public byte[] WriteToBytes(List<Dictionary<string, object>> data)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new System.IO.BinaryWriter(memoryStream, Encoding.UTF8);

        WriteDataToBinary(data, writer);
        return memoryStream.ToArray();
    }

    private void WriteDataToBinary(List<Dictionary<string, object>> data, System.IO.BinaryWriter writer)
    {
        if (data == null || data.Count == 0)
        {
            // 빈 데이터 처리
            writer.Write(0); // 칼럼 갯수
            writer.Write(0); // 데이터 갯수
            return;
        }

        // 칼럼 정보 추출 (첫 번째 행에서)
        var columnInfo = ExtractColumnInfo(data);
        
        // Header 정보 작성
        WriteHeader(columnInfo, data.Count, writer);
        
        // Data 정보 작성 (칼럼별로 저장)
        WriteDataByColumns(data, columnInfo, writer);
    }

    private Dictionary<string, Type> ExtractColumnInfo(List<Dictionary<string, object>> data)
    {
        var columnInfo = new Dictionary<string, Type>();
        
        // 모든 행을 검사하여 칼럼과 타입 정보 수집
        foreach (var row in data)
        {
            foreach (var kvp in row)
            {
                if (!columnInfo.ContainsKey(kvp.Key))
                {
                    columnInfo[kvp.Key] = InferType(kvp.Value);
                }
                else
                {
                    // 기존 타입과 다르면 더 일반적인 타입으로 업그레이드
                    var currentType = columnInfo[kvp.Key];
                    var inferredType = InferType(kvp.Value);
                    columnInfo[kvp.Key] = GetCompatibleType(currentType, inferredType);
                }
            }
        }
        
        return columnInfo;
    }

    private Type InferType(object value)
    {
        if (value == null) return typeof(string); // null은 string으로 기본 처리
        
        return value.GetType();
    }

    private Type GetCompatibleType(Type type1, Type type2)
    {
        if (type1 == type2) return type1;
        
        // 숫자 타입 간 호환성 처리
        if (IsNumericType(type1) && IsNumericType(type2))
        {
            return GetWiderNumericType(type1, type2);
        }
        
        // 그 외의 경우 더 일반적인 타입인 string으로
        return typeof(string);
    }

    private bool IsNumericType(Type type)
    {
        return type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64) ||
               type == typeof(Single) || type == typeof(Double) ||
               type == typeof(decimal);
    }

    private Type GetWiderNumericType(Type type1, Type type2)
    {
        var numericHierarchy = new Type[]
        {
            typeof(Int16), typeof(Int32), typeof(Int64),
            typeof(Single), typeof(Double), typeof(decimal)
        };
        
        int index1 = Array.IndexOf(numericHierarchy, type1);
        int index2 = Array.IndexOf(numericHierarchy, type2);
        
        if (index1 == -1) index1 = numericHierarchy.Length - 1;
        if (index2 == -1) index2 = numericHierarchy.Length - 1;
        
        return numericHierarchy[Math.Max(index1, index2)];
    }

    private void WriteHeader(Dictionary<string, Type> columnInfo, int rowCount, System.IO.BinaryWriter writer)
    {
        // 1. 칼럼 갯수
        writer.Write(columnInfo.Count);
        
        // 2. 데이터 갯수 (행 갯수)
        writer.Write(rowCount);
        
        // 3. 각 칼럼의 메타데이터
        foreach (var kvp in columnInfo)
        {
            // 칼럼 이름
            writer.Write(kvp.Key);
            
            // 칼럼 타입
            writer.Write(kvp.Value.AssemblyQualifiedName ?? kvp.Value.FullName ?? "System.Object");
        }
    }

    private void WriteDataByColumns(List<Dictionary<string, object>> data, Dictionary<string, Type> columnInfo, System.IO.BinaryWriter writer)
    {
        // 각 칼럼별로 데이터 저장
        foreach (var columnKvp in columnInfo)
        {
            WriteColumnData(data, columnKvp.Key, columnKvp.Value, writer);
        }
    }

    private void WriteColumnData(List<Dictionary<string, object>> data, string columnName, Type columnType, System.IO.BinaryWriter writer)
    {
        foreach (var row in data)
        {
            // 해당 칼럼의 값 가져오기
            object value = null;
            if (row.ContainsKey(columnName))
            {
                value = row[columnName];
            }
            
            // null 체크
            if (value == null)
            {
                writer.Write(true); // null 플래그
                continue;
            }
            
            writer.Write(false); // null이 아님을 표시
            
            // 타입별 데이터 저장
            WriteValueByType(value, columnType, writer);
        }
    }

    private void WriteValueByType(object value, Type type, System.IO.BinaryWriter writer)
    {
        try
        {
            if (type == typeof(Int16))
                writer.Write(Convert.ToInt16(value));
            else if (type == typeof(Int32))
                writer.Write(Convert.ToInt32(value));
            else if (type == typeof(Int64))
                writer.Write(Convert.ToInt64(value));
            else if (type == typeof(Single))
                writer.Write(Convert.ToSingle(value));
            else if (type == typeof(Double))
                writer.Write(Convert.ToDouble(value));
            else if (type == typeof(String))
                writer.Write(value.ToString() ?? string.Empty);
            else if (type == typeof(Boolean))
                writer.Write(Convert.ToBoolean(value));
            else if (type.IsArray)
            {
                WriteArrayValue(value, type, writer);
            }
            else if (type.IsEnum)
            {
                // Enum을 정수값으로 저장
                writer.Write(Convert.ToInt32(value));
            }
            else
            {
                // 기타 타입은 문자열로 변환하여 저장
                writer.Write(value.ToString() ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"[BinaryWriter] Error writing value {value} as type {type}: {ex.Message}");
            // 실패 시 문자열로 저장
            writer.Write(value?.ToString() ?? string.Empty);
        }
    }

    private void WriteArrayValue(object value, Type type, System.IO.BinaryWriter writer)
    {
        if (value is Array array)
        {
            // 배열 길이 저장
            writer.Write(array.Length);
            
            // 배열 요소 타입
            var elementType = type.GetElementType();
            if (elementType == null)
            {
                Logger.Instance.Warning($"[BinaryWriter] Could not determine array element type for {type}");
                return;
            }
            
            // 각 요소 저장
            foreach (var element in array)
            {
                if (element == null)
                {
                    writer.Write(true); // null 플래그
                    continue;
                }
                
                writer.Write(false); // null이 아님을 표시
                WriteValueByType(element, elementType, writer);
            }
        }
        else if (value is System.Collections.IEnumerable enumerable)
        {
            // IEnumerable을 배열로 변환
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
                    writer.Write(true); // null 플래그
                    continue;
                }
                
                writer.Write(false); // null이 아님을 표시
                WriteValueByType(item, item.GetType(), writer);
            }
        }
        else
        {
            // 배열이 아닌 경우 빈 배열로 처리
            writer.Write(0);
        }
    }
}

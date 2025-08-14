using System.Text;
using Common.Utils;

namespace Generator.BinaryTable;

/// <summary>
/// 구조화된 바이너리 파일을 Dictionary 형태로 읽어오는 클래스
/// </summary>
public class BinaryReader
{
    /// <summary>
    /// 바이너리 파일에서 Dictionary 형태의 데이터를 읽어옵니다
    /// </summary>
    /// <param name="filePath">읽을 파일 경로</param>
    /// <returns>변환된 데이터 (List&lt;Dictionary&lt;string, object&gt;&gt; 형태)</returns>
    public List<Dictionary<string, object>> ReadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Logger.Instance.Error($"[BinaryReader] File not found: {filePath}");
                return new List<Dictionary<string, object>>();
            }

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new System.IO.BinaryReader(fileStream, Encoding.UTF8);

            var result = ReadDataFromBinary(reader);
            Logger.Instance.Log($"[BinaryReader] Successfully read binary file: {filePath}");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[BinaryReader] Error reading binary file {filePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 바이트 배열에서 Dictionary 형태의 데이터를 읽어옵니다
    /// </summary>
    /// <param name="binaryData">바이너리 데이터</param>
    /// <returns>변환된 데이터 (List&lt;Dictionary&lt;string, object&gt;&gt; 형태)</returns>
    public List<Dictionary<string, object>> ReadFromBytes(byte[] binaryData)
    {
        using var memoryStream = new MemoryStream(binaryData);
        using var reader = new System.IO.BinaryReader(memoryStream, Encoding.UTF8);

        return ReadDataFromBinary(reader);
    }

    private List<Dictionary<string, object>> ReadDataFromBinary(System.IO.BinaryReader reader)
    {
        // Header 정보 읽기
        var fileInfo = ReadHeader(reader);
        
        if (fileInfo.ColumnCount == 0 || fileInfo.RowCount == 0)
        {
            return new List<Dictionary<string, object>>();
        }
        
        // Data 정보 읽기 (칼럼별로 읽기)
        var columnData = ReadDataByColumns(fileInfo, reader);
        
        // Dictionary 형태로 변환
        return ConvertToRowDictionaries(fileInfo, columnData);
    }

    private FileInfo ReadHeader(System.IO.BinaryReader reader)
    {
        var info = new FileInfo();
        
        // 1. 칼럼 갯수
        info.ColumnCount = reader.ReadInt32();
        
        // 2. 데이터 갯수 (행 갯수)
        info.RowCount = reader.ReadInt32();
        
        // 3. 각 칼럼의 메타데이터
        info.Columns = new List<ColumnInfo>();
        for (int i = 0; i < info.ColumnCount; i++)
        {
            var columnInfo = new ColumnInfo();
            
            // 칼럼 이름
            columnInfo.Name = reader.ReadString();
            
            // 칼럼 타입
            var typeString = reader.ReadString();
            columnInfo.Type = GetTypeFromString(typeString);
            
            info.Columns.Add(columnInfo);
        }
        
        return info;
    }

    private Dictionary<string, List<object>> ReadDataByColumns(FileInfo fileInfo, System.IO.BinaryReader reader)
    {
        var columnData = new Dictionary<string, List<object>>();
        
        // 각 칼럼별로 데이터 읽기
        foreach (var columnInfo in fileInfo.Columns)
        {
            var columnValues = new List<object>();
            
            for (int rowIndex = 0; rowIndex < fileInfo.RowCount; rowIndex++)
            {
                // null 체크
                bool isNull = reader.ReadBoolean();
                if (isNull)
                {
                    columnValues.Add(null);
                    continue;
                }
                
                // 타입별 데이터 읽기
                var value = ReadValueByType(columnInfo.Type, reader);
                columnValues.Add(value);
            }
            
            columnData[columnInfo.Name] = columnValues;
        }
        
        return columnData;
    }

    private List<Dictionary<string, object>> ConvertToRowDictionaries(FileInfo fileInfo, Dictionary<string, List<object>> columnData)
    {
        var result = new List<Dictionary<string, object>>();
        
        for (int rowIndex = 0; rowIndex < fileInfo.RowCount; rowIndex++)
        {
            var rowDict = new Dictionary<string, object>();
            
            foreach (var columnInfo in fileInfo.Columns)
            {
                if (columnData.ContainsKey(columnInfo.Name) && 
                    columnData[columnInfo.Name].Count > rowIndex)
                {
                    rowDict[columnInfo.Name] = columnData[columnInfo.Name][rowIndex];
                }
                else
                {
                    rowDict[columnInfo.Name] = null;
                }
            }
            
            result.Add(rowDict);
        }
        
        return result;
    }

    private object ReadValueByType(Type type, System.IO.BinaryReader reader)
    {
        try
        {
            if (type == typeof(Int16))
                return reader.ReadInt16();
            else if (type == typeof(Int32))
                return reader.ReadInt32();
            else if (type == typeof(Int64))
                return reader.ReadInt64();
            else if (type == typeof(Single))
                return reader.ReadSingle();
            else if (type == typeof(Double))
                return reader.ReadDouble();
            else if (type == typeof(String))
                return reader.ReadString();
            else if (type == typeof(Boolean))
                return reader.ReadBoolean();
            else if (type.IsArray)
            {
                return ReadArrayValue(type, reader);
            }
            else if (type.IsEnum)
            {
                // Enum을 정수값에서 복원
                var intValue = reader.ReadInt32();
                return Enum.ToObject(type, intValue);
            }
            else
            {
                // 기타 타입은 문자열에서 변환
                var stringValue = reader.ReadString();
                try
                {
                    return Convert.ChangeType(stringValue, type);
                }
                catch
                {
                    Logger.Instance.Warning($"[BinaryReader] Could not convert '{stringValue}' to {type}");
                    return GetDefaultValue(type);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"[BinaryReader] Error reading value as type {type}: {ex.Message}");
            return GetDefaultValue(type);
        }
    }

    private object ReadArrayValue(Type arrayType, System.IO.BinaryReader reader)
    {
        // 배열 길이 읽기
        int length = reader.ReadInt32();
        
        // 배열 요소 타입
        var elementType = arrayType.GetElementType();
        if (elementType == null)
        {
            Logger.Instance.Warning($"[BinaryReader] Could not determine array element type for {arrayType}");
            return Array.CreateInstance(typeof(object), 0);
        }
        
        // 배열 생성
        var array = Array.CreateInstance(elementType, length);
        
        // 각 요소 읽기
        for (int i = 0; i < length; i++)
        {
            // null 체크
            bool isNull = reader.ReadBoolean();
            if (isNull)
            {
                array.SetValue(GetDefaultValue(elementType), i);
                continue;
            }
            
            var value = ReadValueByType(elementType, reader);
            array.SetValue(value, i);
        }
        
        return array;
    }

    private Type GetTypeFromString(string typeString)
    {
        try
        {
            // AssemblyQualifiedName으로 시도
            var type = Type.GetType(typeString);
            if (type != null)
                return type;
            
            // 기본 타입들 직접 매핑
            return typeString switch
            {
                "System.Int16" => typeof(Int16),
                "System.Int32" => typeof(Int32),
                "System.Int64" => typeof(Int64),
                "System.Single" => typeof(Single),
                "System.Double" => typeof(Double),
                "System.String" => typeof(String),
                "System.Boolean" => typeof(Boolean),
                _ => typeof(String) // 기본값
            };
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"[BinaryReader] Could not resolve type '{typeString}': {ex.Message}");
            return typeof(String);
        }
    }

    private object GetDefaultValue(Type type)
    {
        if (type.IsValueType)
            return Activator.CreateInstance(type) ?? 0;
        return null;
    }

    // Helper classes
    private class FileInfo
    {
        public int ColumnCount { get; set; }
        public int RowCount { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
    }

    private class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public Type Type { get; set; } = typeof(String);
    }
}
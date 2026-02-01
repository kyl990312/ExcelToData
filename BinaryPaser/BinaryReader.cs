using System.Text;
using TableAutomation.Common.Utils;

namespace Generator.BinaryTable;

/// <summary>
/// êµ¬ì¡°?”ëœ ë°”ì´?ˆë¦¬ ?Œì¼??Dictionary ?•íƒœë¡??½ì–´?¤ëŠ” ?´ë˜??
/// </summary>
public class BinaryReader
{
    /// <summary>
    /// ë°”ì´?ˆë¦¬ ?Œì¼?ì„œ Dictionary ?•íƒœ???°ì´?°ë? ?½ì–´?µë‹ˆ??
    /// </summary>
    /// <param name="filePath">?½ì„ ?Œì¼ ê²½ë¡œ</param>
    /// <returns>ë³€?˜ëœ ?°ì´??(List&lt;Dictionary&lt;string, object&gt;&gt; ?•íƒœ)</returns>
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
    /// ë°”ì´??ë°°ì—´?ì„œ Dictionary ?•íƒœ???°ì´?°ë? ?½ì–´?µë‹ˆ??
    /// </summary>
    /// <param name="binaryData">ë°”ì´?ˆë¦¬ ?°ì´??/param>
    /// <returns>ë³€?˜ëœ ?°ì´??(List&lt;Dictionary&lt;string, object&gt;&gt; ?•íƒœ)</returns>
    public List<Dictionary<string, object>> ReadFromBytes(byte[] binaryData)
    {
        using var memoryStream = new MemoryStream(binaryData);
        using var reader = new System.IO.BinaryReader(memoryStream, Encoding.UTF8);

        return ReadDataFromBinary(reader);
    }

    private List<Dictionary<string, object>> ReadDataFromBinary(System.IO.BinaryReader reader)
    {
        // Header ?•ë³´ ?½ê¸°
        var fileInfo = ReadHeader(reader);
        
        if (fileInfo.ColumnCount == 0 || fileInfo.RowCount == 0)
        {
            return new List<Dictionary<string, object>>();
        }
        
        // Data ?•ë³´ ?½ê¸° (ì¹¼ëŸ¼ë³„ë¡œ ?½ê¸°)
        var columnData = ReadDataByColumns(fileInfo, reader);
        
        // Dictionary ?•íƒœë¡?ë³€??
        return ConvertToRowDictionaries(fileInfo, columnData);
    }

    private FileInfo ReadHeader(System.IO.BinaryReader reader)
    {
        var info = new FileInfo();
        
        // 1. ì¹¼ëŸ¼ ê°?ˆ˜
        info.ColumnCount = reader.ReadInt32();
        
        // 2. ?°ì´??ê°?ˆ˜ (??ê°?ˆ˜)
        info.RowCount = reader.ReadInt32();
        
        // 3. ê°?ì¹¼ëŸ¼??ë©”í??°ì´??
        info.Columns = new List<ColumnInfo>();
        for (int i = 0; i < info.ColumnCount; i++)
        {
            var columnInfo = new ColumnInfo();
            
            // ì¹¼ëŸ¼ ?´ë¦„
            columnInfo.Name = reader.ReadString();
            
            // ì¹¼ëŸ¼ ?€??
            var typeString = reader.ReadString();
            columnInfo.Type = GetTypeFromString(typeString);
            
            info.Columns.Add(columnInfo);
        }
        
        return info;
    }

    private Dictionary<string, List<object>> ReadDataByColumns(FileInfo fileInfo, System.IO.BinaryReader reader)
    {
        var columnData = new Dictionary<string, List<object>>();
        
        // ê°?ì¹¼ëŸ¼ë³„ë¡œ ?°ì´???½ê¸°
        foreach (var columnInfo in fileInfo.Columns)
        {
            var columnValues = new List<object>();
            
            for (int rowIndex = 0; rowIndex < fileInfo.RowCount; rowIndex++)
            {
                // null ì²´í¬
                bool isNull = reader.ReadBoolean();
                if (isNull)
                {
                    columnValues.Add(null);
                    continue;
                }
                
                // ?€?…ë³„ ?°ì´???½ê¸°
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
                // Enum???•ìˆ˜ê°’ì—??ë³µì›
                var intValue = reader.ReadInt32();
                return Enum.ToObject(type, intValue);
            }
            else
            {
                // ê¸°í? ?€?…ì? ë¬¸ì?´ì—??ë³€??
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
        // ë°°ì—´ ê¸¸ì´ ?½ê¸°
        int length = reader.ReadInt32();
        
        // ë°°ì—´ ?”ì†Œ ?€??
        var elementType = arrayType.GetElementType();
        if (elementType == null)
        {
            Logger.Instance.Warning($"[BinaryReader] Could not determine array element type for {arrayType}");
            return Array.CreateInstance(typeof(object), 0);
        }
        
        // ë°°ì—´ ?ì„±
        var array = Array.CreateInstance(elementType, length);
        
        // ê°??”ì†Œ ?½ê¸°
        for (int i = 0; i < length; i++)
        {
            // null ì²´í¬
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
            // AssemblyQualifiedName?¼ë¡œ ?œë„
            var type = Type.GetType(typeString);
            if (type != null)
                return type;
            
            // ê¸°ë³¸ ?€?…ë“¤ ì§ì ‘ ë§¤í•‘
            return typeString switch
            {
                "System.Int16" => typeof(Int16),
                "System.Int32" => typeof(Int32),
                "System.Int64" => typeof(Int64),
                "System.Single" => typeof(Single),
                "System.Double" => typeof(Double),
                "System.String" => typeof(String),
                "System.Boolean" => typeof(Boolean),
                _ => typeof(String) // ê¸°ë³¸ê°?
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
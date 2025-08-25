namespace ExcelParser;
public static class TableDefine
{
    public const int ROW_HEADER_NAME = 1;
    public const int ROW_HEADER_TYPE = 2;
    public const string REGEX_START_EXCEPT_COLUMN = "#";
    public const string REGEX_START_EXCEPT_ROW = "//";
}

public static class TableEx
{
}

public enum EDataType
{
    Int16,
    Int32,
    Int64,
    Single,
    Double,
    String,
    DateTime,
    Date,
    UserDefinedEnum,    // 사용자 정의 enum
    Array,              // 배열 타입 (다른 타입과 조합)
    
    Max
}

public static class DataTypeHelper
{
    /// <summary>
    /// 문자열 타입명을 EDataType으로 변환
    /// </summary>
    public static (EDataType dataType, string customTypeName, bool isArray) ParseDataType(string typeString)
    {
        if (string.IsNullOrEmpty(typeString))
            return (EDataType.String, string.Empty, false);
        
        bool isArray = typeString.EndsWith("[]");
        string baseType = isArray ? typeString.Replace("[]", "") : typeString;
        
        var dataType = baseType switch
        {
            "Int16" => EDataType.Int16,
            "Int32" => EDataType.Int32,
            "Int64" => EDataType.Int64,
            "Single" => EDataType.Single,
            "Double" => EDataType.Double,
            "String" => EDataType.String,
            "DateTime" => EDataType.DateTime,
            "Date" => EDataType.Date,
            _ => EDataType.UserDefinedEnum
        };
        
        string customTypeName = dataType == EDataType.UserDefinedEnum ? baseType : string.Empty;
        return (dataType, customTypeName, isArray);
    }
    
    /// <summary>
    /// EDataType을 실제 System.Type으로 변환
    /// </summary>
    public static Type GetSystemType(EDataType dataType, bool isArray = false)
    {
        var baseType = dataType switch
        {
            EDataType.Int16 => typeof(short),
            EDataType.Int32 => typeof(int),
            EDataType.Int64 => typeof(long),
            EDataType.Single => typeof(float),
            EDataType.Double => typeof(double),
            EDataType.String => typeof(string),
            EDataType.DateTime => typeof(DateTime),
            EDataType.Date => typeof(DateTime), // 또는 DateOnly if .NET 6+
            EDataType.UserDefinedEnum => typeof(object),
            _ => typeof(object)
        };
        
        return isArray ? typeof(string) : baseType; // 배열은 일단 string으로 저장
    }
    
    /// <summary>
    /// 기본값 반환
    /// </summary>
    public static object GetDefaultValue(EDataType dataType, bool isArray = false)
    {
        if (isArray)
            return Array.CreateInstance(typeof(object), 0);
            
        return dataType switch
        {
            EDataType.Int16 => (short)0,
            EDataType.Int32 => 0,
            EDataType.Int64 => 0L,
            EDataType.Single => 0.0f,
            EDataType.Double => 0.0,
            EDataType.String => string.Empty,
            EDataType.DateTime => DateTime.MinValue,
            EDataType.Date => DateTime.MinValue,
            EDataType.UserDefinedEnum => 0, // enum의 기본값은 0
            _ => null
        };
    }
    
    /// <summary>
    /// 숫자 타입인지 확인
    /// </summary>
    public static bool IsNumericType(EDataType dataType)
    {
        return dataType == EDataType.Int16 ||
               dataType == EDataType.Int32 ||
               dataType == EDataType.Int64 ||
               dataType == EDataType.Single ||
               dataType == EDataType.Double;
    }
}
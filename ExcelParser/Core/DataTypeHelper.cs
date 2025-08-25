namespace ExcelParser.Core;

public enum EDataType
{
    Int16,
    Int32,
    Int64,
    Single,
    Double,
    String,
    UserDefinedEnum,  // 사용자 정의 Enum
    
    Max
}

public static class DataTypeHelper
{
    /// <summary>
    /// 문자열 타입 정의를 파싱합니다.
    /// </summary>
    /// <param name="typeStr">타입 문자열 (예: "System.Int32", "PlayerClass", "System.String[]")</param>
    /// <returns>(데이터타입, 사용자정의타입명, 배열여부)</returns>
    public static (EDataType dataType, string customTypeName, bool isArray) ParseDataType(string typeStr)
    {
        if (string.IsNullOrEmpty(typeStr))
            return (EDataType.String, string.Empty, false);

        bool isArray = typeStr.EndsWith("[]");
        string baseType = isArray ? typeStr.Replace("[]", "") : typeStr;

        // System. 접두사 제거
        if (baseType.StartsWith("System."))
            baseType = baseType.Substring(7);

        return baseType switch
        {
            "Int16" => (EDataType.Int16, string.Empty, isArray),
            "Int32" => (EDataType.Int32, string.Empty, isArray),
            "Int64" => (EDataType.Int64, string.Empty, isArray),
            "Single" => (EDataType.Single, string.Empty, isArray),
            "Double" => (EDataType.Double, string.Empty, isArray),
            "String" => (EDataType.String, string.Empty, isArray),
            _ => (EDataType.UserDefinedEnum, baseType, isArray) // 사용자 정의 타입으로 처리
        };
    }

    /// <summary>
    /// EDataType을 System.Type으로 변환합니다.
    /// </summary>
    public static Type GetSystemType(EDataType dataType, bool isArray = false)
    {
        Type baseType = dataType switch
        {
            EDataType.Int16 => typeof(Int16),
            EDataType.Int32 => typeof(Int32),
            EDataType.Int64 => typeof(Int64),
            EDataType.Single => typeof(Single),
            EDataType.Double => typeof(Double),
            EDataType.String => typeof(String),
            EDataType.UserDefinedEnum => typeof(int), // Enum은 기본적으로 int로 처리
            _ => typeof(object)
        };

        return isArray ? baseType.MakeArrayType() : baseType;
    }

    /// <summary>
    /// 사용자 정의 Enum 타입을 실제 Type으로 변환합니다.
    /// </summary>
    public static Type GetEnumType(string typeName)
    {
        // 현재 로드된 어셈블리에서 타입 검색
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type != null && type.IsEnum)
                return type;
        }

        // 타입을 찾을 수 없으면 int로 대체
        return typeof(int);
    }

    /// <summary>
    /// 문자열을 지정된 타입으로 변환합니다.
    /// </summary>
    public static object ConvertValue(string value, EDataType dataType, string customTypeName = "", bool isArray = false)
    {
        if (isArray)
        {
            return ConvertToArray(value, dataType, customTypeName);
        }

        return dataType switch
        {
            EDataType.Int16 => Convert.ToInt16(value),
            EDataType.Int32 => Convert.ToInt32(value),
            EDataType.Int64 => Convert.ToInt64(value),
            EDataType.Single => Convert.ToSingle(value),
            EDataType.Double => Convert.ToDouble(value),
            EDataType.String => value,
            EDataType.UserDefinedEnum => ConvertToEnum(value, customTypeName),
            _ => value
        };
    }

    private static object ConvertToArray(string arrayStr, EDataType elementType, string customTypeName)
    {
        var elements = arrayStr.Trim('[', ']').Split(',').Select(s => s.Trim()).ToArray();
        
        return elementType switch
        {
            EDataType.Int16 => elements.Select(x=>Int16.Parse(x)).ToArray(),
            EDataType.Int32 => elements.Select(x=>Int32.Parse(x)).ToArray(),
            EDataType.Int64 => elements.Select(x=>Int64.Parse(x)).ToArray(),
            EDataType.Single => elements.Select(Convert.ToSingle).ToArray(),
            EDataType.Double => elements.Select(Convert.ToDouble).ToArray(),
            EDataType.String => elements,
            EDataType.UserDefinedEnum => elements.Select(e => ConvertToEnum(e, customTypeName)).ToArray(),
            _ => elements
        };
    }

    private static object ConvertToEnum(string value, string enumTypeName)
    {
        if (int.TryParse(value, out int intValue))
        {
            return intValue; // Enum 값을 int로 반환
        }

        // Enum 이름으로 변환 (실제 Enum 타입이 있다면)
        var enumType = GetEnumType(enumTypeName);
        if (enumType != typeof(int) && Enum.TryParse(enumType, value, out var enumValue))
        {
            return enumValue;
        }

        return 0; // 기본값
    }
}

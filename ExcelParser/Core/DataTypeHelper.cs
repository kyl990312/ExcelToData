using Common.Utils;
using UserEnum;

namespace ExcelParser.Core;

public enum EDataType
{
    Int16,
    Int32,
    Int64,
    Single,
    Double,
    String,
    DateTime,
    UserDefinedEnum,
    
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
            "DateTime" => (EDataType.DateTime, string.Empty, isArray),
            _ => (EDataType.UserDefinedEnum, baseType, isArray)
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
            EDataType.DateTime => typeof(DateTime),
            EDataType.UserDefinedEnum => typeof(string), // Enum은 문자열로 저장
            _ => typeof(object)
        };

        return isArray ? baseType.MakeArrayType() : baseType;
    }

    /// <summary>
    /// EDataType의 기본값을 반환합니다.
    /// </summary>
    public static object GetDefaultValue(EDataType dataType, bool isArray = false)
    {
        if (isArray)
        {
            return dataType switch
            {
                EDataType.Int16 => Array.Empty<short>(),
                EDataType.Int32 => Array.Empty<int>(),
                EDataType.Int64 => Array.Empty<long>(),
                EDataType.Single => Array.Empty<float>(),
                EDataType.Double => Array.Empty<double>(),
                EDataType.String => Array.Empty<string>(),
                EDataType.DateTime => Array.Empty<DateTime>(),
                EDataType.UserDefinedEnum => Array.Empty<string>(), // Enum 배열도 문자열
                _ => Array.Empty<object>()
            };
        }

        return dataType switch
        {
            EDataType.Int16 => (short)0,
            EDataType.Int32 => 0,
            EDataType.Int64 => 0L,
            EDataType.Single => 0.0f,
            EDataType.Double => 0.0,
            EDataType.String => string.Empty,
            EDataType.DateTime => DateTime.MinValue,
            EDataType.UserDefinedEnum => string.Empty, // Enum 기본값도 빈 문자열
            _ => null
        };
    }

    /// <summary>
    /// System.Type의 기본값을 반환합니다.
    /// </summary>
    public static object GetDefaultValue(Type type)
    {
        if (type == typeof(Int16)) return (short)0;
        if (type == typeof(Int32)) return 0;
        if (type == typeof(Int64)) return 0L;
        if (type == typeof(Single)) return 0.0f;
        if (type == typeof(Double)) return 0.0;
        if (type == typeof(String)) return string.Empty;
        if (type == typeof(DateTime)) return DateTime.MinValue;
        if (type.IsArray)
        {
            var elementType = type.GetElementType() ?? typeof(object);
            return Array.CreateInstance(elementType, 0);
        }
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.Length > 0 ? values.GetValue(0) : Enum.ToObject(type, 0);
        }
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// 문자열을 EDataType에 맞게 변환합니다.
    /// </summary>
    public static object ConvertValue(string value, EDataType dataType, string customTypeName = "", bool isArray = false)
    {
        if (string.IsNullOrEmpty(value))
            return GetDefaultValue(dataType, isArray);

        try
        {
            if (isArray)
                return ConvertToArray(value, dataType, customTypeName);

            return dataType switch
            {
                EDataType.Int16 => short.TryParse(value, out var s) ? s : (short)0,
                EDataType.Int32 => int.TryParse(value, out var i) ? i : 0,
                EDataType.Int64 => long.TryParse(value, out var l) ? l : 0L,
                EDataType.Single => float.TryParse(value, out var f) ? f : 0.0f,
                EDataType.Double => double.TryParse(value, out var d) ? d : 0.0,
                EDataType.String => value,
                EDataType.DateTime => DateTime.TryParse(value, out var dt) ? dt : DateTime.MinValue,
                EDataType.UserDefinedEnum => ConvertToUserEnumString(value, customTypeName),
                _ => value
            };
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"[DataTypeHelper] Error converting '{value}' to {dataType}: {ex.Message}");
            return GetDefaultValue(dataType, isArray);
        }
    }

    /// <summary>
    /// 문자열을 배열로 변환합니다.
    /// </summary>
    private static object ConvertToArray(string arrayStr, EDataType elementType, string customTypeName)
    {
        var elements = arrayStr.Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        if (elements.Length == 0)
            return GetDefaultValue(elementType, isArray: true);

        return elementType switch
        {
            EDataType.Int16 => elements.Select(x => short.TryParse(x, out var v) ? v : (short)0).ToArray(),
            EDataType.Int32 => elements.Select(x => int.TryParse(x, out var v) ? v : 0).ToArray(),
            EDataType.Int64 => elements.Select(x => long.TryParse(x, out var v) ? v : 0L).ToArray(),
            EDataType.Single => elements.Select(x => float.TryParse(x, out var v) ? v : 0.0f).ToArray(),
            EDataType.Double => elements.Select(x => double.TryParse(x, out var v) ? v : 0.0).ToArray(),
            EDataType.String => elements,
            EDataType.DateTime => elements.Select(x => DateTime.TryParse(x, out var v) ? v : DateTime.MinValue).ToArray(),
            EDataType.UserDefinedEnum => elements.Select(e => ConvertToUserEnumString(e, customTypeName)).ToArray(),
            _ => elements
        };
    }

    /// <summary>
    /// 사용자 정의 Enum 값을 문자열(멤버명)로 변환합니다.
    /// 입력이 숫자면 EnumDB에서 멤버명을 조회하고, 문자열이면 유효성 검증 후 그대로 반환합니다.
    /// </summary>
    private static string ConvertToUserEnumString(string value, string enumTypeName)
    {
        // 숫자로 입력된 경우 → 멤버명으로 변환
        if (int.TryParse(value, out int intValue))
        {
            var memberName = EnumDB.Instance.GetEnumMemberName(enumTypeName, intValue);
            if (!string.IsNullOrEmpty(memberName))
                return memberName;
            
            Logger.Instance.Warning($"[DataTypeHelper] Unknown enum value '{intValue}' for type '{enumTypeName}'. Using empty string.");
            return string.Empty;
        }

        // 문자열로 입력된 경우 → 유효성 검증 후 그대로 반환
        var enumValue = EnumDB.Instance.GetEnumValue(enumTypeName, value);
        if (enumValue.HasValue)
            return value; // 유효한 멤버명이면 그대로 반환

        Logger.Instance.Warning($"[DataTypeHelper] Unknown enum member '{value}' for type '{enumTypeName}'. Using as-is.");
        return value; // 검증 실패해도 입력값 그대로 반환 (유연성)
    }

    /// <summary>
    /// 사용자 정의 Enum 타입인지 EnumDB에서 검증합니다.
    /// </summary>
    public static bool IsValidUserDefinedEnum(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;
        return EnumDB.Instance.HasEnum(typeName);
    }
    
    /// <summary>
    /// Enum 멤버명을 int 값으로 변환합니다. (런타임에서 사용)
    /// </summary>
    public static int GetEnumValueFromString(string enumTypeName, string memberName)
    {
        var value = EnumDB.Instance.GetEnumValue(enumTypeName, memberName);
        return value ?? 0;
    }
}

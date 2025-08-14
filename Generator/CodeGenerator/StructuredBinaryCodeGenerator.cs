using System.Text;
using Common.Utils;
using BinaryParser;

namespace Generator.CodeGenerator;

/// <summary>
/// 구조화된 바이너리 파일에서 C# 클래스 코드를 생성하는 클래스
/// </summary>
public class StructuredBinaryCSharpGenerator : ICodeGenerator
{
    private readonly CSharpClassParam _param;
    private readonly BinaryParser.BinaryReader _binaryReader;

    public StructuredBinaryCSharpGenerator(CSharpClassParam param)
    {
        _param = param;
        _binaryReader = new BinaryParser.BinaryReader();
    }

    public void Generate(string binaryFilePath, string outputPath)
    {
        if (!File.Exists(binaryFilePath))
        {
            Logger.Instance.Error($"[StructuredBinaryCSharpGenerator] Binary file not found: {binaryFilePath}");
            return;
        }

        try
        {
            // 구조화된 바이너리 파일을 Dictionary 형태로 읽기
            var data = _binaryReader.ReadFromFile(binaryFilePath);
            
            if (data.Count == 0)
            {
                Logger.Instance.Warning($"[StructuredBinaryCSharpGenerator] No data found in file: {binaryFilePath}");
                return;
            }

            // Dictionary 데이터를 기반으로 C# 클래스 생성
            GenerateClassFromData(data, outputPath);
            
            Logger.Instance.Log($"[StructuredBinaryCSharpGenerator] Generated code: {outputPath}");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[StructuredBinaryCSharpGenerator] Error generating code: {ex.Message}");
            throw;
        }
    }

    private void GenerateClassFromData(List<Dictionary<string, object>> data, string outputPath)
    {
        var className = Path.GetFileNameWithoutExtension(outputPath);
        var sb = new StringBuilder();
        
        // 클래스 스키마 분석
        var classSchema = AnalyzeClassSchema(data);
        
        // 파일 헤더 및 네임스페이스
        sb.AppendLine("// 자동 생성된 코드 - 수정하지 마세요");
        sb.AppendLine("// 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("// 소스: 구조화된 바이너리 파일");
        sb.AppendLine($"// 데이터 행 수: {data.Count}");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_param.@namespace}");
        sb.AppendLine("{");
        
        // 클래스 정의
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {className} 데이터 클래스");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public class {className}");
        sb.AppendLine("    {");
        
        // 기본 생성자
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// 기본 생성자");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public {className}()");
        sb.AppendLine("        {");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // 매개변수 있는 생성자
        GenerateParameterizedConstructor(sb, className, classSchema);
        
        // 프로퍼티들 생성
        GenerateProperties(sb, classSchema);
        
        // 클래스 및 네임스페이스 닫기
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        // 디렉토리 확인 및 생성
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // 파일 저장
        File.WriteAllText(outputPath, sb.ToString());
    }

    private Dictionary<string, PropertyInfo> AnalyzeClassSchema(List<Dictionary<string, object>> data)
    {
        var schema = new Dictionary<string, PropertyInfo>();
        
        // 모든 행을 분석하여 각 칼럼의 타입 정보 수집
        foreach (var row in data)
        {
            foreach (var kvp in row)
            {
                if (!schema.ContainsKey(kvp.Key))
                {
                    schema[kvp.Key] = new PropertyInfo
                    {
                        Name = kvp.Key,
                        Type = InferPropertyType(kvp.Value),
                        IsNullable = kvp.Value == null
                    };
                }
                else
                {
                    var propertyInfo = schema[kvp.Key];
                    
                    // null 가능성 체크
                    if (kvp.Value == null)
                    {
                        propertyInfo.IsNullable = true;
                    }
                    
                    // 타입 호환성 체크
                    if (kvp.Value != null)
                    {
                        var currentType = InferPropertyType(kvp.Value);
                        propertyInfo.Type = GetCompatibleType(propertyInfo.Type, currentType);
                    }
                }
            }
        }
        
        return schema;
    }

    private Type InferPropertyType(object value)
    {
        if (value == null) return typeof(string);
        
        var type = value.GetType();
        
        // 배열 타입 처리
        if (type.IsArray)
        {
            return type;
        }
        
        // 기본 타입들
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
            type == typeof(bool) || type == typeof(string) || type == typeof(DateTime))
        {
            return type;
        }
        
        // 그 외는 string으로 처리
        return typeof(string);
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
        return type == typeof(short) || type == typeof(int) || type == typeof(long) ||
               type == typeof(float) || type == typeof(double) || type == typeof(decimal);
    }

    private Type GetWiderNumericType(Type type1, Type type2)
    {
        var numericHierarchy = new Type[]
        {
            typeof(short), typeof(int), typeof(long),
            typeof(float), typeof(double), typeof(decimal)
        };
        
        int index1 = Array.IndexOf(numericHierarchy, type1);
        int index2 = Array.IndexOf(numericHierarchy, type2);
        
        if (index1 == -1) index1 = 0;
        if (index2 == -1) index2 = 0;
        
        return numericHierarchy[Math.Max(index1, index2)];
    }

    private void GenerateParameterizedConstructor(StringBuilder sb, string className, Dictionary<string, PropertyInfo> schema)
    {
        sb.AppendLine($"        /// <summary>");
        sb.AppendLine($"        /// 모든 필드 초기화 생성자");
        sb.AppendLine($"        /// </summary>");
        sb.AppendLine($"        public {className}(");
        
        // 생성자 매개변수
        var parameters = schema.Values.ToList();
        for (int i = 0; i < parameters.Count; i++)
        {
            var property = parameters[i];
            var paramName = GetCamelCaseName(property.Name);
            var paramType = GetCSharpTypeName(property.Type, property.IsNullable);
            
            sb.Append($"            {paramType} {paramName}");
            
            if (i < parameters.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }
        
        sb.AppendLine("        )");
        sb.AppendLine("        {");
        
        // 생성자 내에서 프로퍼티 초기화
        foreach (var property in schema.Values)
        {
            var paramName = GetCamelCaseName(property.Name);
            sb.AppendLine($"            {property.Name} = {paramName};");
        }
        
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private void GenerateProperties(StringBuilder sb, Dictionary<string, PropertyInfo> schema)
    {
        foreach (var property in schema.Values)
        {
            var propertyType = GetCSharpTypeName(property.Type, property.IsNullable);
            var defaultValue = GetDefaultValueString(property.Type, property.IsNullable);
            
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// {property.Name} 프로퍼티");
            sb.AppendLine($"        /// 타입: {property.Type.Name}");
            if (property.IsNullable)
            {
                sb.AppendLine($"        /// Nullable: Yes");
            }
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {propertyType} {property.Name} {{ get; private set; }}{defaultValue}");
            sb.AppendLine();
        }
    }

    private string GetCSharpTypeName(Type type, bool isNullable)
    {
        string typeName = type switch
        {
            Type t when t == typeof(short) => "short",
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(long) => "long",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(decimal) => "decimal",
            Type t when t == typeof(bool) => "bool",
            Type t when t == typeof(string) => "string",
            Type t when t == typeof(DateTime) => "DateTime",
            Type t when t.IsArray => GetArrayTypeName(t),
            _ => type.Name
        };
        
        // Nullable 타입 처리 (값 타입만)
        if (isNullable && type.IsValueType && !type.IsArray)
        {
            typeName += "?";
        }
        
        return typeName;
    }

    private string GetArrayTypeName(Type arrayType)
    {
        var elementType = arrayType.GetElementType();
        if (elementType == null) return "object[]";
        
        var elementTypeName = elementType switch
        {
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(string) => "string",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(float) => "float",
            Type t when t == typeof(bool) => "bool",
            _ => elementType.Name
        };
        
        return elementTypeName + "[]";
    }

    private string GetDefaultValueString(Type type, bool isNullable)
    {
        if (type == typeof(string))
        {
            return isNullable ? " = null;" : " = string.Empty;";
        }
        
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            if (elementType != null)
            {
                var elementTypeName = GetCSharpTypeName(elementType, false);
                return isNullable ? " = null;" : $" = Array.Empty<{elementTypeName}>();";
            }
            return " = Array.Empty<object>();";
        }
        
        if (isNullable && type.IsValueType)
        {
            return " = null;";
        }
        
        if (!type.IsValueType && isNullable)
        {
            return " = null!;";
        }
        
        return string.Empty;
    }

    private string GetCamelCaseName(string pascalCaseName)
    {
        if (string.IsNullOrEmpty(pascalCaseName))
            return pascalCaseName;
            
        return char.ToLower(pascalCaseName[0]) + pascalCaseName.Substring(1);
    }

    private class PropertyInfo
    {
        public string Name { get; set; } = string.Empty;
        public Type Type { get; set; } = typeof(string);
        public bool IsNullable { get; set; }
    }
}



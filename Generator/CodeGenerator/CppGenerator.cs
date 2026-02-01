using System.Text;
using BinaryParser;

namespace Generator.CodeGenerator
{
    public struct CppClassParam
    {
        public string headerGuardPrefix;
        public bool generateToString;
        public bool generateEquals;
        public bool generateOperators;
        public bool useStdString;  // std::string 사용 여부 (false면 const char*)
    }
    
    /// <summary>
    /// Binary 파일에서 C++ 클래스를 생성하는 생성기
    /// 헤더 파일(.h)과 소스 파일(.cpp)을 각각 생성합니다.
    /// </summary>
    public class CppClassGenerator : CodeGeneratorBase
    {
        private readonly CppClassParam _param;
        
        public CppClassGenerator(CppClassParam param)
        {
            _param = param;
        }
        
        protected override void GenerateCode(BinarySchema schema, string outputPath)
        {
            var className = Path.GetFileNameWithoutExtension(outputPath);
            var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            
            // 헤더 파일 생성
            var headerPath = Path.Combine(directory, $"{className}.h");
            GenerateHeaderFile(schema, className, headerPath);
            
            // 소스 파일 생성
            var sourcePath = Path.Combine(directory, $"{className}.cpp");
            GenerateSourceFile(schema, className, sourcePath);
        }
        
        private void GenerateHeaderFile(BinarySchema schema, string className, string headerPath)
        {
            var sb = new StringBuilder();
            var guardName = GetHeaderGuardName(className);
            
            GenerateHeaderGuardStart(sb, guardName);
            GenerateIncludes(sb, schema.Columns);
            GenerateClassDeclaration(sb, className, schema.Columns);
            GenerateHeaderGuardEnd(sb, guardName);
            
            File.WriteAllText(headerPath, sb.ToString());
        }
        
        private void GenerateSourceFile(BinarySchema schema, string className, string sourcePath)
        {
            var sb = new StringBuilder();
            
            GenerateSourceHeader(sb, className);
            GenerateConstructorImplementations(sb, className, schema.Columns);
            
            if (_param.generateToString)
                GenerateToStringImplementation(sb, className, schema.Columns);
            
            if (_param.generateEquals)
                GenerateEqualsImplementation(sb, className, schema.Columns);
            
            if (_param.generateOperators)
                GenerateOperatorImplementations(sb, className, schema.Columns);
            
            File.WriteAllText(sourcePath, sb.ToString());
        }
        
        private string GetHeaderGuardName(string className)
        {
            var prefix = string.IsNullOrEmpty(_param.headerGuardPrefix) 
                ? "" 
                : $"{_param.headerGuardPrefix.ToUpperInvariant()}_";
            return $"{prefix}{className.ToUpperInvariant()}_H";
        }
        
        private void GenerateHeaderGuardStart(StringBuilder sb, string guardName)
        {
            sb.AppendLine("// 자동 생성된 코드 - 수정하지 마세요");
            sb.AppendLine($"// 생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"#ifndef {guardName}");
            sb.AppendLine($"#define {guardName}");
            sb.AppendLine();
        }
        
        private void GenerateHeaderGuardEnd(StringBuilder sb, string guardName)
        {
            sb.AppendLine();
            sb.AppendLine($"#endif // {guardName}");
        }
        
        private void GenerateIncludes(StringBuilder sb, Dictionary<string, string> columns)
        {
            sb.AppendLine("#include <cstdint>");
            
            bool hasString = columns.Values.Any(t => 
                t.Contains("String") || t.Contains("string"));
            bool hasArray = columns.Values.Any(t => t.Contains("[]"));
            
            if (hasString && _param.useStdString)
                sb.AppendLine("#include <string>");
            
            if (hasArray)
                sb.AppendLine("#include <vector>");
            
            if (_param.generateToString)
                sb.AppendLine("#include <sstream>");
            
            sb.AppendLine();
        }
        
        private void GenerateClassDeclaration(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            sb.AppendLine($"/**");
            sb.AppendLine($" * @brief {className} 데이터 클래스");
            sb.AppendLine($" */");
            sb.AppendLine($"class {className}");
            sb.AppendLine("{");
            sb.AppendLine("public:");
            
            // 기본 생성자 선언
            sb.AppendLine($"    /**");
            sb.AppendLine($"     * @brief 기본 생성자");
            sb.AppendLine($"     */");
            sb.AppendLine($"    {className}();");
            sb.AppendLine();
            
            // 매개변수 생성자 선언
            sb.AppendLine($"    /**");
            sb.AppendLine($"     * @brief 모든 필드 초기화 생성자");
            sb.AppendLine($"     */");
            sb.Append($"    {className}(");
            
            int paramIndex = 0;
            foreach (var column in columns)
            {
                string cppType = ConvertToCppType(column.Value);
                string paramType = GetParameterType(cppType);
                sb.Append($"{paramType} {ToSnakeCase(column.Key)}");
                
                if (paramIndex < columns.Count - 1)
                    sb.Append(", ");
                
                paramIndex++;
            }
            sb.AppendLine(");");
            sb.AppendLine();
            
            // 소멸자
            sb.AppendLine($"    /**");
            sb.AppendLine($"     * @brief 소멸자");
            sb.AppendLine($"     */");
            sb.AppendLine($"    ~{className}() = default;");
            sb.AppendLine();
            
            // Getter 메서드 선언
            foreach (var column in columns)
            {
                string cppType = ConvertToCppType(column.Value);
                string returnType = GetReturnType(cppType);
                string methodName = $"Get{column.Key}";
                
                sb.AppendLine($"    /**");
                sb.AppendLine($"     * @brief {column.Key} 값을 반환합니다.");
                sb.AppendLine($"     */");
                sb.AppendLine($"    {returnType} {methodName}() const {{ return {ToMemberName(column.Key)}; }}");
                sb.AppendLine();
            }
            
            // ToString 메서드 선언
            if (_param.generateToString)
            {
                sb.AppendLine($"    /**");
                sb.AppendLine($"     * @brief 문자열 표현을 반환합니다.");
                sb.AppendLine($"     */");
                sb.AppendLine($"    std::string ToString() const;");
                sb.AppendLine();
            }
            
            // Equals 메서드 선언
            if (_param.generateEquals)
            {
                sb.AppendLine($"    /**");
                sb.AppendLine($"     * @brief 다른 객체와의 동등성을 비교합니다.");
                sb.AppendLine($"     */");
                sb.AppendLine($"    bool Equals(const {className}& other) const;");
                sb.AppendLine();
            }
            
            // 연산자 선언
            if (_param.generateOperators)
            {
                sb.AppendLine($"    bool operator==(const {className}& other) const;");
                sb.AppendLine($"    bool operator!=(const {className}& other) const;");
                sb.AppendLine();
            }
            
            // private 멤버 변수
            sb.AppendLine("private:");
            foreach (var column in columns)
            {
                string cppType = ConvertToCppType(column.Value);
                sb.AppendLine($"    {cppType} {ToMemberName(column.Key)};");
            }
            
            sb.AppendLine("};");
        }
        
        private void GenerateSourceHeader(StringBuilder sb, string className)
        {
            sb.AppendLine("// 자동 생성된 코드 - 수정하지 마세요");
            sb.AppendLine($"// 생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"#include \"{className}.h\"");
            sb.AppendLine();
        }
        
        private void GenerateConstructorImplementations(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            // 기본 생성자 구현
            sb.AppendLine($"{className}::{className}()");
            sb.Append("    : ");
            
            int index = 0;
            foreach (var column in columns)
            {
                string cppType = ConvertToCppType(column.Value);
                string defaultValue = GetDefaultValue(cppType);
                sb.Append($"{ToMemberName(column.Key)}({defaultValue})");
                
                if (index < columns.Count - 1)
                {
                    sb.AppendLine(",");
                    sb.Append("      ");
                }
                
                index++;
            }
            sb.AppendLine();
            sb.AppendLine("{");
            sb.AppendLine("}");
            sb.AppendLine();
            
            // 매개변수 생성자 구현
            sb.Append($"{className}::{className}(");
            
            int paramIndex = 0;
            foreach (var column in columns)
            {
                string cppType = ConvertToCppType(column.Value);
                string paramType = GetParameterType(cppType);
                sb.Append($"{paramType} {ToSnakeCase(column.Key)}");
                
                if (paramIndex < columns.Count - 1)
                    sb.Append(", ");
                
                paramIndex++;
            }
            sb.AppendLine(")");
            sb.Append("    : ");
            
            index = 0;
            foreach (var column in columns)
            {
                sb.Append($"{ToMemberName(column.Key)}({ToSnakeCase(column.Key)})");
                
                if (index < columns.Count - 1)
                {
                    sb.AppendLine(",");
                    sb.Append("      ");
                }
                
                index++;
            }
            sb.AppendLine();
            sb.AppendLine("{");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        private void GenerateToStringImplementation(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            sb.AppendLine($"std::string {className}::ToString() const");
            sb.AppendLine("{");
            sb.AppendLine("    std::ostringstream oss;");
            sb.Append($"    oss << \"{className}{{\"");
            
            int index = 0;
            foreach (var column in columns)
            {
                if (index > 0)
                    sb.Append($"\n        << \", \"");
                
                sb.Append($"\n        << \"{column.Key}=\" << {ToMemberName(column.Key)}");
                index++;
            }
            
            sb.AppendLine();
            sb.AppendLine("        << \"}\";");
            sb.AppendLine("    return oss.str();");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        private void GenerateEqualsImplementation(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            sb.AppendLine($"bool {className}::Equals(const {className}& other) const");
            sb.AppendLine("{");
            sb.Append("    return ");
            
            int index = 0;
            foreach (var column in columns)
            {
                if (index > 0)
                {
                    sb.AppendLine(" &&");
                    sb.Append("           ");
                }
                
                sb.Append($"{ToMemberName(column.Key)} == other.{ToMemberName(column.Key)}");
                index++;
            }
            
            sb.AppendLine(";");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        private void GenerateOperatorImplementations(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            sb.AppendLine($"bool {className}::operator==(const {className}& other) const");
            sb.AppendLine("{");
            sb.AppendLine("    return Equals(other);");
            sb.AppendLine("}");
            sb.AppendLine();
            
            sb.AppendLine($"bool {className}::operator!=(const {className}& other) const");
            sb.AppendLine("{");
            sb.AppendLine("    return !Equals(other);");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        /// <summary>
        /// 타입 문자열을 C++ 타입으로 변환합니다
        /// </summary>
        private string ConvertToCppType(string typeString)
        {
            // AssemblyQualifiedName에서 타입명만 추출
            var normalizedType = typeString.Contains(",") 
                ? typeString.Split(',')[0].Trim()
                : typeString;
            
            return normalizedType switch
            {
                "System.Int16" or "Int16" or "short" => "int16_t",
                "System.Int32" or "Int32" or "int" => "int32_t",
                "System.Int64" or "Int64" or "long" => "int64_t",
                "System.Single" or "Single" or "float" => "float",
                "System.Double" or "Double" or "double" => "double",
                "System.String" or "String" or "string" => _param.useStdString ? "std::string" : "const char*",
                "System.Boolean" or "Boolean" or "bool" => "bool",
                "System.DateTime" or "DateTime" => "int64_t",  // timestamp로 처리
                "System.Int16[]" or "Int16[]" or "short[]" => "std::vector<int16_t>",
                "System.Int32[]" or "Int32[]" or "int[]" => "std::vector<int32_t>",
                "System.Int64[]" or "Int64[]" or "long[]" => "std::vector<int64_t>",
                "System.Single[]" or "Single[]" or "float[]" => "std::vector<float>",
                "System.Double[]" or "Double[]" or "double[]" => "std::vector<double>",
                "System.String[]" or "String[]" or "string[]" => _param.useStdString ? "std::vector<std::string>" : "std::vector<const char*>",
                _ => "int32_t"  // Enum이나 기타 타입은 int32_t로 처리
            };
        }
        
        /// <summary>
        /// 매개변수 타입을 반환합니다 (const 참조 여부 결정)
        /// </summary>
        private string GetParameterType(string cppType)
        {
            // string이나 vector는 const 참조로 전달
            if (cppType.StartsWith("std::") || cppType == "const char*")
                return $"const {cppType}&";
            
            return cppType;
        }
        
        /// <summary>
        /// 반환 타입을 결정합니다
        /// </summary>
        private string GetReturnType(string cppType)
        {
            // string이나 vector는 const 참조로 반환
            if (cppType.StartsWith("std::"))
                return $"const {cppType}&";
            
            return cppType;
        }
        
        /// <summary>
        /// 타입의 기본값을 반환합니다
        /// </summary>
        private string GetDefaultValue(string cppType)
        {
            if (cppType == "bool")
                return "false";
            if (cppType.StartsWith("std::vector"))
                return "";
            if (cppType == "std::string")
                return "";
            if (cppType == "const char*")
                return "nullptr";
            if (cppType == "float" || cppType == "double")
                return "0.0";
            
            return "0";
        }
        
        /// <summary>
        /// PascalCase를 snake_case로 변환합니다
        /// </summary>
        private string ToSnakeCase(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        sb.Append('_');
                    sb.Append(char.ToLower(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// 멤버 변수명을 생성합니다 (m_ prefix)
        /// </summary>
        private string ToMemberName(string name)
        {
            return $"m_{ToSnakeCase(name)}";
        }
    }
}

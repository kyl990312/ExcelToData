using System.Text;
using BinaryParser;

namespace Generator.CodeGenerator
{
    public struct CSharpClassParam
    {
        public string @namespace;
        public bool generateToString;
        public bool generateEquals;
        public bool generateGetHashCode;
    }
    
    /// <summary>
    /// Binary 파일에서 C# 클래스를 생성하는 기존 생성기
    /// </summary>
    public class CSharpClassGenerator : CodeGeneratorBase
    {
        private readonly CSharpClassParam _param;
        
        public CSharpClassGenerator(CSharpClassParam param)
        {
            _param = param;
        }
        
        protected override void GenerateCode(BinarySchema schema, string outputPath)
        {
            var className = Path.GetFileNameWithoutExtension(outputPath);
            var sb = new StringBuilder();
            
            GenerateClassHeader(sb, className);
            GenerateConstructors(sb, className, schema.Columns);
            GenerateProperties(sb, schema.Columns);
            
            if (_param.generateToString)
                GenerateToStringMethod(sb, schema.Columns);
            
            if (_param.generateEquals)
                GenerateEqualsMethod(sb, className, schema.Columns);
                
            if (_param.generateGetHashCode)
                GenerateGetHashCodeMethod(sb, schema.Columns);
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // 파일 저장
            File.WriteAllText(outputPath, sb.ToString());
        }
        
        private void GenerateClassHeader(StringBuilder sb, string className)
        {
            sb.AppendLine("// 자동 생성된 코드 - 수정하지 마세요");
            sb.AppendLine("// 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine($"namespace {_param.@namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {className} 데이터 클래스");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");
        }
        
        private void GenerateConstructors(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            // 기본 생성자
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 기본 생성자");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {className}()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // 매개변수 있는 생성자
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 모든 필드 초기화 생성자");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public {className}(");
            
            int paramCount = 0;
            foreach (var item in columns)
            {
                string propertyType = ConvertToCSharpType(item.Value);
                sb.Append($"            {propertyType} {char.ToLower(item.Key[0])}{item.Key.Substring(1)}");
                
                if (paramCount < columns.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
                
                paramCount++;
            }
            
            sb.AppendLine("        )");
            sb.AppendLine("        {");
            
            foreach (var item in columns)
            {
                string paramName = char.ToLower(item.Key[0]) + item.Key.Substring(1);
                sb.AppendLine($"            {item.Key} = {paramName};");
            }
            
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void GenerateProperties(StringBuilder sb, Dictionary<string, string> columns)
        {
            foreach (var item in columns)
            {
                string propertyType = ConvertToCSharpType(item.Value);
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// {item.Key} 프로퍼티");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public {propertyType} {item.Key} {{ get; private set; }}");
                sb.AppendLine();
            }
        }
        
        private void GenerateToStringMethod(StringBuilder sb, Dictionary<string, string> columns)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 문자열 표현을 반환합니다.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public override string ToString()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return $\"{{{string.Join(", ", columns.Keys.Select(k => $"{k}={{{k}}}"))}\";");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void GenerateEqualsMethod(StringBuilder sb, string className, Dictionary<string, string> columns)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 다른 객체와의 동등성을 비교합니다.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public override bool Equals(object? obj)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (obj is not {className} other) return false;");
            sb.AppendLine($"            return {string.Join(" && ", columns.Keys.Select(k => $"Equals({k}, other.{k})"))};");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void GenerateGetHashCodeMethod(StringBuilder sb, Dictionary<string, string> columns)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 해시 코드를 반환합니다.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public override int GetHashCode()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return HashCode.Combine({string.Join(", ", columns.Keys)});");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        /// <summary>
        /// 타입 문자열을 C# 타입으로 변환합니다
        /// </summary>
        private string ConvertToCSharpType(string typeString)
        {
            // AssemblyQualifiedName에서 타입명만 추출
            var normalizedType = typeString.Contains(",") 
                ? typeString.Split(',')[0].Trim()
                : typeString;
            
            return normalizedType switch
            {
                "System.Int16" or "Int16" or "short" => "short",
                "System.Int32" or "Int32" or "int" => "int",
                "System.Int64" or "Int64" or "long" => "long",
                "System.Single" or "Single" or "float" => "float",
                "System.Double" or "Double" or "double" => "double",
                "System.String" or "String" or "string" => "string",
                "System.Boolean" or "Boolean" or "bool" => "bool",
                "System.DateTime" or "DateTime" => "DateTime",
                "System.Int16[]" or "Int16[]" or "short[]" => "short[]",
                "System.Int32[]" or "Int32[]" or "int[]" => "int[]",
                "System.Int64[]" or "Int64[]" or "long[]" => "long[]",
                "System.Single[]" or "Single[]" or "float[]" => "float[]",
                "System.Double[]" or "Double[]" or "double[]" => "double[]",
                "System.String[]" or "String[]" or "string[]" => "string[]",
                _ => normalizedType  // Enum이나 기타 커스텀 타입은 그대로 사용
            };
        }
    }
}

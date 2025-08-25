using System.Text;
using System.Text.Json;

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
        
        protected override void GenerateCode(Dictionary<string, JsonElement> data, string outputPath)
        {
            var className = Path.GetFileNameWithoutExtension(outputPath);
            var sb = new StringBuilder();
            
            GenerateClassHeader(sb, className);
            GenerateConstructors(sb, className, data);
            GenerateProperties(sb, data);
            
            if (_param.generateToString)
                GenerateToStringMethod(sb, data);
            
            if (_param.generateEquals)
                GenerateEqualsMethod(sb, className, data);
                
            if (_param.generateGetHashCode)
                GenerateGetHashCodeMethod(sb, data);
            
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
        
        private void GenerateConstructors(StringBuilder sb, string className, Dictionary<string, JsonElement> data)
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
            foreach (var item in data)
            {
                string propertyType = DeterminePropertyType(item.Value);
                sb.Append($"            {propertyType} {char.ToLower(item.Key[0])}{item.Key.Substring(1)}");
                
                if (paramCount < data.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
                
                paramCount++;
            }
            
            sb.AppendLine("        )");
            sb.AppendLine("        {");
            
            foreach (var item in data)
            {
                string paramName = char.ToLower(item.Key[0]) + item.Key.Substring(1);
                sb.AppendLine($"            {item.Key} = {paramName};");
            }
            
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void GenerateProperties(StringBuilder sb, Dictionary<string, JsonElement> data)
        {
            foreach (var item in data)
            {
                string propertyType = DeterminePropertyType(item.Value);
                sb.AppendLine($"        /// <summary>");
                sb.AppendLine($"        /// {item.Key} 프로퍼티");
                sb.AppendLine($"        /// </summary>");
                sb.AppendLine($"        public {propertyType} {item.Key} {{ get; private set; }}");
                sb.AppendLine();
            }
        }
        
        private void GenerateToStringMethod(StringBuilder sb, Dictionary<string, JsonElement> data)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 문자열 표현을 반환합니다.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public override string ToString()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return $\"{{{string.Join(", ", data.Keys.Select(k => $"{k}={{{k}}}"))}\";");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void GenerateEqualsMethod(StringBuilder sb, string className, Dictionary<string, JsonElement> data)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 다른 객체와의 동등성을 비교합니다.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public override bool Equals(object? obj)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (obj is not {className} other) return false;");
            sb.AppendLine($"            return {string.Join(" && ", data.Keys.Select(k => $"Equals({k}, other.{k})"))};");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void GenerateGetHashCodeMethod(StringBuilder sb, Dictionary<string, JsonElement> data)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// 해시 코드를 반환합니다.");
            sb.AppendLine($"        /// </summary>");
            sb.AppendLine($"        public override int GetHashCode()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return HashCode.Combine({string.Join(", ", data.Keys)});");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private string DeterminePropertyType(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => DetermineArrayType(element),
                JsonValueKind.Object => "Dictionary<string, object>",
                JsonValueKind.String => "string",
                JsonValueKind.Number => DetermineNumberType(element),
                JsonValueKind.True or JsonValueKind.False => "bool",
                JsonValueKind.Null => "object",
                _ => "object"
            };
        }
        
        private string DetermineArrayType(JsonElement element)
        {
            if (element.GetArrayLength() == 0)
                return "object[]";
                
            var firstElement = element[0];
            return firstElement.ValueKind switch
            {
                JsonValueKind.String => "string[]",
                JsonValueKind.Number => firstElement.TryGetInt32(out _) ? "int[]" : "double[]",
                JsonValueKind.True or JsonValueKind.False => "bool[]",
                JsonValueKind.Object => "Dictionary<string, object>[]",
                _ => "object[]"
            };
        }
        
        private string DetermineNumberType(JsonElement element)
        {
            if (element.TryGetInt32(out _))
                return "int";
                
            if (element.TryGetInt64(out _))
                return "long";
                
            return "double";
        }
    }
}

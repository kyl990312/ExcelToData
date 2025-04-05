using System.Text;
using System.Text.Json;

namespace Generator.CodeGenerator
{
    public struct CSharpClassParam
    {
        public string @namespace;
    }
    
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
            
            // 파일 헤더 및 네임스페이스
            sb.AppendLine("// 자동 생성된 코드 - 수정하지 마세요");
            sb.AppendLine("// 생성 시간: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine($"namespace {_param.@namespace}");
            sb.AppendLine("{");
            
            // 클래스 정의
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");
            
            // 생성자 추가
            sb.AppendLine($"        // 기본 생성자");
            sb.AppendLine($"        public {className}()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // 매개변수 있는 생성자 추가
            sb.AppendLine($"        // 모든 필드 초기화 생성자");
            sb.AppendLine($"        public {className}(");
            
            // 생성자 매개변수
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
            
            // 생성자 내에서 프로퍼티 초기화
            foreach (var item in data)
            {
                string paramName = char.ToLower(item.Key[0]) + item.Key.Substring(1);
                sb.AppendLine($"            {item.Key} = {paramName};");
            }
            
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // 데이터 항목을 프로퍼티로 변환 (private setter)
            foreach (var item in data)
            {
                string propertyType = DeterminePropertyType(item.Value);
                sb.AppendLine($"        public {propertyType} {item.Key} {{ get; private set; }}");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // 파일 저장
            File.WriteAllText(outputPath, sb.ToString());
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
            // 배열의 첫 번째 요소를 기준으로 타입 결정 (배열이 비어있으면 object 배열로 가정)
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
            // 정수 vs 부동소수점 구분
            if (element.TryGetInt32(out _))
                return "int";
                
            if (element.TryGetInt64(out _))
                return "long";
                
            return "double";
        }
    }
}
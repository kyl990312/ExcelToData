// Generator 프로젝트에 추가될 코드 생성 시스템

using Common.Utils;
using System.Text.Json;
using ExcelParser;

namespace Generator.CodeGenerator
{
    // 코드 생성기 인터페이스
    public interface ICodeGenerator
    {
        void Generate(string binaryFilePath, string outputPath);
    }

    // 코드 생성기 기본 클래스
    public abstract class CodeGeneratorBase : ICodeGenerator
    {
        BinaryParser.BinaryReader _binaryReader = new ();
        public void Generate(string binaryFilePath, string outputPath)
        {
            if (!File.Exists(binaryFilePath))
            {
                Logger.Instance.Error($"[CodeGenerator] Binary file not found: {binaryFilePath}");
                return;
            }

            try
            {
                var table = _binaryReader.ReadFromFile(binaryFilePath);
                string jsonString = TableHelper.ConverDataTableToJSON(TableHelper.ConvertBinaryToDataTable(table));
                using var document = JsonDocument.Parse(jsonString);

                // 루트 요소 검사 (객체 또는 배열)
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // 객체인 경우 직접 처리
                    ProcessJsonObject(document.RootElement, outputPath);
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // 배열인 경우 첫 번째 요소 처리
                    if (document.RootElement.GetArrayLength() > 0)
                    {
                        var firstElement = document.RootElement[0];
                        if (firstElement.ValueKind == JsonValueKind.Object)
                        {
                            ProcessJsonObject(firstElement, outputPath);
                        }
                        else
                        {
                            Logger.Instance.Error(
                                $"[CodeGenerator] First array element is not an object: {binaryFilePath}");
                            return;
                        }
                    }
                    else
                    {
                        Logger.Instance.Error($"[CodeGenerator] JSON array is empty: {binaryFilePath}");
                        return;
                    }
                }
                else
                {
                    Logger.Instance.Error(
                        $"[CodeGenerator] Root element is neither object nor array: {binaryFilePath}");
                    return;
                }
            }
            catch (JsonException ex)
            {
                Logger.Instance.Error(
                    $"[CodeGenerator] Failed to parse binary data: {binaryFilePath}. Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"[CodeGenerator] Error generating code: {ex.Message}");
            }
        }

        private void ProcessJsonObject(JsonElement jsonObject, string outputPath)
        {
            // JsonElement를 처리할 Dictionary로 변환
            var data = new Dictionary<string, JsonElement>();
            foreach (var property in jsonObject.EnumerateObject())
            {
                data[property.Name] = property.Value.Clone();
            }

            // 디렉토리 확인 및 생성
            string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 실제 코드 생성 (자식 클래스에서 구현)
            GenerateCode(data, outputPath);

            Logger.Instance.Log($"[CodeGenerator] Generated code: {outputPath}");
        }

        protected abstract void GenerateCode(Dictionary<string, JsonElement> data, string outputPath);
    }
}
using Common.Utils;
using BinaryParser;
using ExcelParser;

namespace Generator.CodeGenerator
{
    // 코드 생성기 인터페이스
    public interface ICodeGenerator
    {
        void Generate(string binaryFilePath, string outputPath);
    }

    // CustomDataTable 기반 코드 생성기 인터페이스
    public interface ITableCodeGenerator
    {
        void Generate(CustomDataTable table, string outputPath);
    }

    // 코드 생성기 기본 클래스
    public abstract class CodeGeneratorBase : ICodeGenerator
    {
        public void Generate(string binaryFilePath, string outputPath)
        {
            if (!File.Exists(binaryFilePath))
            {
                Logger.Instance.Error($"[CodeGenerator] Binary file not found: {binaryFilePath}");
                return;
            }

            try
            {
                // 바이너리 파일 로드
                byte[] binaryData = File.ReadAllBytes(binaryFilePath);

                // BinarySchema를 사용하여 스키마 정보 읽기
                var schema = BinarySchema.ReadFromBytes(binaryData);

                if (schema.ColumnCount == 0)
                {
                    Logger.Instance.Error($"[CodeGenerator] Binary schema is empty: {binaryFilePath}");
                    return;
                }

                // 디렉토리 확인 및 생성
                string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 실제 코드 생성 (자식 클래스에서 구현)
                GenerateCode(schema, outputPath);

                Logger.Instance.Log($"[CodeGenerator] Generated code: {outputPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"[CodeGenerator] Error generating code: {ex.Message}");
            }
        }

        protected abstract void GenerateCode(BinarySchema schema, string outputPath);
    }
}

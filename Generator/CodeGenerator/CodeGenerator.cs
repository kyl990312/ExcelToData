using TableAutomation.Common.Utils;
using BinaryParser;
using ExcelParser;

namespace Generator.CodeGenerator
{
    // ì½”ë“œ ?ì„±ê¸??¸í„°?˜ì´??
    public interface ICodeGenerator
    {
        void Generate(string binaryFilePath, string outputPath);
    }

    // CustomDataTable ê¸°ë°˜ ì½”ë“œ ?ì„±ê¸??¸í„°?˜ì´??
    public interface ITableCodeGenerator
    {
        void Generate(CustomDataTable table, string outputPath);
    }

    // ì½”ë“œ ?ì„±ê¸?ê¸°ë³¸ ?´ë˜??
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
                // ë°”ì´?ˆë¦¬ ?Œì¼ ë¡œë“œ
                byte[] binaryData = File.ReadAllBytes(binaryFilePath);

                // BinarySchemaë¥??¬ìš©?˜ì—¬ ?¤í‚¤ë§??•ë³´ ?½ê¸°
                var schema = BinarySchema.ReadFromBytes(binaryData);

                if (schema.ColumnCount == 0)
                {
                    Logger.Instance.Error($"[CodeGenerator] Binary schema is empty: {binaryFilePath}");
                    return;
                }

                // ?”ë ‰? ë¦¬ ?•ì¸ ë°??ì„±
                string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // ?¤ì œ ì½”ë“œ ?ì„± (?ì‹ ?´ë˜?¤ì—??êµ¬í˜„)
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

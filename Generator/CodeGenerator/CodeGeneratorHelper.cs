using System.Text.Json;
using Common.Utils;

namespace Generator.CodeGenerator
{
    /// <summary>
    /// 코드 생성 설정을 관리하는 헬퍼 클래스
    /// </summary>
    public static class CodeGenerateHelper
    {
        /// <summary>
        /// 코드 생성 설정을 파일에서 로드
        /// </summary>
        /// <param name="filePath">설정 파일 경로</param>
        /// <returns>코드 생성 설정 인스턴스</returns>
        public static CodeGenerateConfig? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logger.Instance.Warning($"[CodeGenerateHelper] Config file not found: {filePath}");
                    return null;
                }

                string configJson = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                };

                var config = JsonSerializer.Deserialize<CodeGenerateConfig>(configJson, options);
                if (config == null)
                {
                    Logger.Instance.Error($"[CodeGenerateHelper] Failed to deserialize config: {filePath}");
                    return null;
                }

                Logger.Instance.Log($"[CodeGenerateHelper] Loaded config from: {filePath}");
                return config;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"[CodeGenerateHelper] Error loading config: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 코드 생성 설정을 파일로 저장
        /// </summary>
        /// <param name="filePath">저장할 파일 경로</param>
        /// <param name="config">저장할 설정 인스턴스</param>
        /// <returns>성공 여부</returns>
        public static bool SaveToFile(string filePath, CodeGenerateConfig config)
        {
            try
            {
                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string configJson = JsonSerializer.Serialize(config, options);
                File.WriteAllText(filePath, configJson);

                Logger.Instance.Log($"[CodeGenerateHelper] Saved config to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"[CodeGenerateHelper] Error saving config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 기본 설정으로 저장
        /// </summary>
        /// <param name="filePath">저장할 파일 경로</param>
        /// <returns>성공 여부</returns>
        public static bool SaveDefaultConfig(string filePath)
        {
            var defaultConfig = new CodeGenerateConfig
            {
                BinaryDirectory = "Binary",
                ClassOutputDirectory = "Generated/Classes",
                ManagerOutputDirectory = "Generated/Managers",
                ClassNamespace = "Generated.Data",
                ManagerNamespace = "Generated.Managers"
            };

            return SaveToFile(filePath, defaultConfig);
        }

        /// <summary>
        /// 모든 출력 디렉토리를 확인하고 필요한 경우 생성
        /// </summary>
        public static void EnsureDirectoriesExist(CodeGenerateConfig config)
        {
            if (!Directory.Exists(config.ClassOutputDirectory))
                Directory.CreateDirectory(config.ClassOutputDirectory);

            if (!Directory.Exists(config.ManagerOutputDirectory))
                Directory.CreateDirectory(config.ManagerOutputDirectory);
        }
    }
}
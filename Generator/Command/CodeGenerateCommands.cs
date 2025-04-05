using Common.Command;
using Common.Utils;

namespace Generator.CodeGenerator
{
    /// <summary>
    /// 코드 생성 명령어 클래스
    /// </summary>
    public class GenerateCSharpCodeCommand : Command
    {
        private readonly string _configPath;
        
        public GenerateCSharpCodeCommand(string configPath)
        {
            _configPath = configPath;
        }
        
        public override void Execute(out string result)
        {
            var config = CodeGenerateHelper.LoadFromFile(_configPath);
            
            if (config == null)
            {
                result = $"[GenerateCodeCommand] Failed to load config from: {_configPath}";
                return;
            }
            
            if (!Directory.Exists(config.BinaryDirectory))
            {
                result = $"[GenerateCodeCommand] Binary directory not found: {config.BinaryDirectory}";
                return;
            }
            
            try
            {
                // 디렉토리 준비
                CodeGenerateHelper.EnsureDirectoriesExist(config);
                
                // 클래스 생성
                int classCount = GenerateClasses(config);
                result = $"[GenerateCodeCommand] Generated {classCount} classes.";
            }
            catch (Exception ex)
            {
                result = $"[GenerateCodeCommand] Error: {ex.Message}";
                Logger.Instance.Error($"[GenerateCodeCommand] Exception: {ex}");
            }
        }
        
        private int GenerateClasses(CodeGenerateConfig config)
        {
            int count = 0;
            
            // 모든 바이너리 파일 검색
            var files = Directory.GetFiles(config.BinaryDirectory, config.ReadFileExtension, SearchOption.AllDirectories);
            
            // 클래스 생성기 생성
            var param = new CSharpClassParam()
            {
                @namespace = config.ClassNamespace,
            };
            var generator = new CSharpClassGenerator(param);
            
            foreach (var file in files)
            {
                try
                {
                    // 출력 파일 경로
                    string relativePath = Path.GetRelativePath(config.BinaryDirectory, file);
                    string outputPath = Path.Combine(
                        config.ClassOutputDirectory,
                        Path.ChangeExtension(relativePath, ".cs")
                    );
                    
                    // 코드 생성
                    generator.Generate(file, outputPath);
                    Logger.Instance.Log($"[GenerateCodeCommand] Generated {relativePath}");
                    count++;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"[GenerateCodeCommand] Error generating class for {file}: {ex.Message}");
                }
            }
            
            return count;
        }
    }

    /// <summary>
    /// 코드 생성 설정 생성 명령어
    /// </summary>
    public class GenerateCodeConfigCommand : Command
    {
        private readonly string _configPath;
        
        public GenerateCodeConfigCommand(string configPath)
        {
            _configPath = configPath;
        }
        
        public override void Execute(out string result)
        {
            try
            {
                // 기본 설정 파일 생성
                bool success = CodeGenerateHelper.SaveDefaultConfig(_configPath);
                
                if (success)
                {
                    result = $"[GenerateCodeConfigCommand] Created config file: {_configPath}";
                }
                else
                {
                    result = $"[GenerateCodeConfigCommand] Failed to create config file: {_configPath}";
                }
            }
            catch (Exception ex)
            {
                result = $"[GenerateCodeConfigCommand] Error: {ex.Message}";
                Logger.Instance.Error($"[GenerateCodeConfigCommand] Exception: {ex}");
            }
        }
    }
}
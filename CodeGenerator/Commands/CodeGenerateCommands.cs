using ClosedXML.Excel;
using Common.Command;
using Common.Utils;
using Generator.CodeGenerator;
using Generator.EnumGenerator;

namespace CodeGenerator;

/// <summary>
/// 코드 생성 명령어 클래스
/// </summary>
public class GenerateCSharpCodeCommand : Command
{
    private readonly CodeGenerateConfig _config;

    public GenerateCSharpCodeCommand(CodeGenerateConfig config)
    {
        _config = config;
    }

    public override void Execute(out string result)
    {
        if (!Directory.Exists(_config.BinaryDirectory))
        {
            result = $"[GenerateCodeCommand] Binary directory not found: {_config.BinaryDirectory}";
            return;
        }

        try
        {
            // directory 준비
            if (!Directory.Exists(_config.ClassOutputDirectory))
                Directory.CreateDirectory(_config.ClassOutputDirectory);

            if (!Directory.Exists(_config.ManagerOutputDirectory))
                Directory.CreateDirectory(_config.ManagerOutputDirectory);

            // 클래스 생성
            int classCount = GenerateClasses(_config);
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
            var config = CodeGenerateConfig.GetDefault();
            bool success = FileLoader.SaveJson(_configPath, config);
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

/// <summary>
/// 구조화된 바이너리용 C# 데이터 클래스 생성 명령어
/// </summary>
public class GenerateStructuredBinaryCSharpCommand : Command
{
    private readonly string _configPath;

    public GenerateStructuredBinaryCSharpCommand(string configPath)
    {
        _configPath = configPath;
    }

    public override void Execute(out string result)
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                result = $"[GenerateStructuredBinaryCSharpCommand] Config file not found: {_configPath}";
                return;
            }

            // 설정 로드 (CodeGenerateConfig 사용)
            if (!Common.Command.FileLoader.LoadAndConvertJson(_configPath, out CodeGenerator.CodeGenerateConfig config))
            {
                result = $"[GenerateStructuredBinaryCSharpCommand] Failed to load config: {_configPath}";
                return;
            }

            if (!Directory.Exists(config.BinaryDirectory))
            {
                result = $"[GenerateStructuredBinaryCSharpCommand] Binary directory not found: {config.BinaryDirectory}";
                return;
            }

            // 출력 디렉토리 준비
            if (!Directory.Exists(config.ClassOutputDirectory))
                Directory.CreateDirectory(config.ClassOutputDirectory);

            // 구조화된 바이너리 파일들 처리
            var files = Directory.GetFiles(config.BinaryDirectory, "*.sbin", SearchOption.AllDirectories);
            var param = new CSharpClassParam { @namespace = config.ClassNamespace };
            var generator = new StructuredBinaryCSharpGenerator(param);

            int count = 0;
            foreach (var file in files)
            {
                try
                {
                    string relativePath = Path.GetRelativePath(config.BinaryDirectory, file);
                    string outputPath = Path.Combine(config.ClassOutputDirectory, Path.ChangeExtension(relativePath, ".cs"));
                    
                    generator.Generate(file, outputPath);
                    count++;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"[GenerateStructuredBinaryCSharpCommand] Error processing {file}: {ex.Message}");
                }
            }

            result = $"[GenerateStructuredBinaryCSharpCommand] Generated {count} classes from structured binary files.";
        }
        catch (Exception ex)
        {
            result = $"[GenerateStructuredBinaryCSharpCommand] Error: {ex.Message}";
            Logger.Instance.Error($"[GenerateStructuredBinaryCSharpCommand] Exception: {ex}");
        }
    }
}



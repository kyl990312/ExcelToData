using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomation.Common.Utils;
using Generator.CodeGenerator;
using ExcelParser;
using TableAutomarionToolShared;
using UserEnum;

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
            generateEquals = true,
            generateGetHashCode = true
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
/// C++ 코드 생성 명령어 클래스
/// </summary>
public class GenerateCppCodeCommand : Command
{
    private readonly CodeGenerateConfig _config;

    public GenerateCppCodeCommand(CodeGenerateConfig config)
    {
        _config = config;
    }

    public override void Execute(out string result)
    {
        if (!Directory.Exists(_config.BinaryDirectory))
        {
            result = $"[GenerateCppCodeCommand] Binary directory not found: {_config.BinaryDirectory}";
            return;
        }

        try
        {
            // directory 준비
            if (!Directory.Exists(_config.ClassOutputDirectory))
                Directory.CreateDirectory(_config.ClassOutputDirectory);

            // 클래스 생성
            int classCount = GenerateClasses(_config);
            result = $"[GenerateCppCodeCommand] Generated {classCount} C++ classes.";
        }
        catch (Exception ex)
        {
            result = $"[GenerateCppCodeCommand] Error: {ex.Message}";
            Logger.Instance.Error($"[GenerateCppCodeCommand] Exception: {ex}");
        }
    }

    private int GenerateClasses(CodeGenerateConfig config)
    {
        int count = 0;

        // 모든 바이너리 파일 검색
        var files = Directory.GetFiles(config.BinaryDirectory, config.ReadFileExtension, SearchOption.AllDirectories);

        // C++ 클래스 생성기 생성
        var param = new CppClassParam()
        {
            headerGuardPrefix = config.ClassNamespace.Replace(".", "_").ToUpperInvariant(),
            generateToString = true,
            generateEquals = true,
            generateOperators = true,
            useStdString = true
        };
        var generator = new CppClassGenerator(param);

        foreach (var file in files)
        {
            try
            {
                // 출력 파일 경로 (.h와 .cpp가 함께 생성됨)
                string relativePath = Path.GetRelativePath(config.BinaryDirectory, file);
                string outputPath = Path.Combine(
                    config.ClassOutputDirectory,
                    Path.ChangeExtension(relativePath, ".h")
                );

                // 코드 생성
                generator.Generate(file, outputPath);
                Logger.Instance.Log($"[GenerateCppCodeCommand] Generated {relativePath}");
                count++;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"[GenerateCppCodeCommand] Error generating class for {file}: {ex.Message}");
            }
        }

        return count;
    }
}

/// <summary>
/// Enum 코드(.cs)를 생성하는 커맨드
/// 시스템 통일 경로(SystemPath.EnumBinaryPath)에서 EnumCache를 로드하여 코드를 생성합니다.
/// </summary>
public class GenerateEnumCodeCommand : Command
{
    private readonly string _outputDirectory;
    private readonly string _codeExtension;

    public GenerateEnumCodeCommand(string outputDirectory, string codeExtension)
    {
        _outputDirectory = outputDirectory;
        _codeExtension = codeExtension;
    }
    
    public override void Execute(out string result)
    {
        result = "";

        // 시스템 통일 경로에서 EnumDB 로드
        var enumBinaryPath = SystemPath.EnumBinaryPath;

        if (!File.Exists(enumBinaryPath))
        {
            result = $"[GenerateEnumCodeCommand] Enum 바이너리 파일을 찾을 수 없습니다: {enumBinaryPath}";
            Logger.Instance.Warning(result);
            return;
        }

        try
        {
            if (!EnumDB.Instance.LoadFromBinary(enumBinaryPath))
            {
                result = $"[GenerateEnumCodeCommand] EnumCache 로드 실패: {enumBinaryPath}";
                Logger.Instance.Error(result);
                return;
            }

            if (EnumDB.Instance.IsEmpty)
            {
                result = "[GenerateEnumCodeCommand] EnumCache가 비어있습니다.";
                Logger.Instance.Warning(result);
                return;
            }

            var enumGenerator = new EnumCodeGenerator();
            enumGenerator.Generate(EnumDB.Instance.Cache, _outputDirectory, _codeExtension);
            
            result = $"[GenerateEnumCodeCommand] Enum 코드 생성 완료: {_outputDirectory} ({EnumDB.Instance.EnumCount}개 Enum)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[GenerateEnumCodeCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
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

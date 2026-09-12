using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.Common.Utils;
using TableAutomationTool.Generator.CodeGenerator;
using TableAutomationTool.ExcelParser;
using TableAutomationTool.Shared;
using TableAutomationTool.UserEnum;

namespace TableAutomationTool.CodeGenerator;

/// <summary>
/// 코드 생성 명령어 클래스
/// </summary>
public class GenerateCSharpCodeCommand : Command
{
    private readonly CodeGenerateConfig _config;
    private readonly string? _targetFileName;

    public GenerateCSharpCodeCommand(CodeGenerateConfig config, string? targetFileName = null)
    {
        _config = config;
        _targetFileName = targetFileName;
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

        // targetFileName이 지정되어 있으면 해당 파일만 처리, 없으면 모든 파일 처리
        string[] files;
        if (!string.IsNullOrEmpty(_targetFileName))
        {
            // 확장자 없이 파일명이 들어오므로 .bin 파일 찾기
            string targetFile = Path.Combine(config.BinaryDirectory, $"{_targetFileName}.bin");
            if (File.Exists(targetFile))
            {
                files = new[] { targetFile };
                Logger.Instance.Log($"[GenerateCodeCommand] Processing specific file: {_targetFileName}");
            }
            else
            {
                Logger.Instance.Warning($"[GenerateCodeCommand] Target file not found: {targetFile}");
                files = Array.Empty<string>();
            }
        }
        else
        {
            files = Directory.GetFiles(config.BinaryDirectory, config.ReadFileExtension, SearchOption.AllDirectories);
            Logger.Instance.Log($"[GenerateCodeCommand] Processing all files in directory");
        }

        // EnumDB 는 테이블이 아니라 Enum 메타데이터다. 클래스 생성 대상에서 제외한다.
        // (기본 배치상 BinaryDirectory 안에 함께 놓여 "*.bin" 글롭에 딸려 들어온다)
        foreach (var skipped in files.Where(config.IsEnumBinary))
            Logger.Instance.Log($"[GenerateCodeCommand] Skip enum metadata (not a table): {Path.GetFileName(skipped)}");
        files = files.Where(f => !config.IsEnumBinary(f)).ToArray();

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
    private readonly string? _targetFileName;

    public GenerateCppCodeCommand(CodeGenerateConfig config, string? targetFileName = null)
    {
        _config = config;
        _targetFileName = targetFileName;
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

        // targetFileName이 지정되어 있으면 해당 파일만 처리, 없으면 모든 파일 처리
        string[] files;
        if (!string.IsNullOrEmpty(_targetFileName))
        {
            // 확장자 없이 파일명이 들어오므로 .bin 파일 찾기
            string targetFile = Path.Combine(config.BinaryDirectory, $"{_targetFileName}.bin");
            if (File.Exists(targetFile))
            {
                files = new[] { targetFile };
                Logger.Instance.Log($"[GenerateCppCodeCommand] Processing specific file: {_targetFileName}");
            }
            else
            {
                Logger.Instance.Warning($"[GenerateCppCodeCommand] Target file not found: {targetFile}");
                files = Array.Empty<string>();
            }
        }
        else
        {
            files = Directory.GetFiles(config.BinaryDirectory, config.ReadFileExtension, SearchOption.AllDirectories);
            Logger.Instance.Log($"[GenerateCppCodeCommand] Processing all files in directory");
        }

        // EnumDB 는 테이블이 아니라 Enum 메타데이터다. 클래스 생성 대상에서 제외한다.
        foreach (var skipped in files.Where(config.IsEnumBinary))
            Logger.Instance.Log($"[GenerateCppCodeCommand] Skip enum metadata (not a table): {Path.GetFileName(skipped)}");
        files = files.Where(f => !config.IsEnumBinary(f)).ToArray();

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
        // init-config 를 비파괴(멱등)로 만든다. 사용자가 수정한 설정을 덮어쓰지 않는다.
        if (File.Exists(_configPath))
        {
            result = $"[GenerateCodeConfigCommand] Config file already exists at: {_configPath}";
            return;
        }

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

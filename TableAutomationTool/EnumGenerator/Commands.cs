using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.Common.Utils;
using System.Text;
using TableAutomationTool.UserEnum;
using Exception = System.Exception;
using TableAutomationTool.Shared;

namespace TableAutomationTool.EnumGenerator;

public class CreateEnumGenerateConfigCommand : Command
{
    private string _path;
    public CreateEnumGenerateConfigCommand(string path)
    {
        _path = path;
    }
    
    public override void Execute(out string resultMsg)
    {
        resultMsg = string.Empty;
        // init-config 를 비파괴(멱등)로 만든다. 사용자가 수정한 설정을 덮어쓰지 않는다.
        if (File.Exists(_path))
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Config file already exists at: {_path}";
            return;
        }

        var config = EnumGeneratorConfig.GetDefault();
        if (FileLoader.SaveJson(_path, config))
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Success Create Config : {_path}";
        }
        else
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Fail to Create Config : {_path}";
        }
    }
}

/// <summary>
/// 프로그래머가 작성한 C# enum 소스 파일(SystemEnumFiles)에서 EnumCache를 생성하는 커맨드.
/// 파일 하나라도 읽지 못하거나 값 계산에 실패하면 예외를 던져 파이프라인을 멈춘다.
/// (실패를 로그만 남기고 넘어가면 이전 EnumDB.bin 으로 데이터 추출이 진행되어 잘못된 검증을 하게 된다)
/// </summary>
public class EnumCacheFromSourceCommand : Command
{
    private readonly string[] _sourceFiles;

    public EnumCacheFromSourceCommand(string[] sourceFiles)
    {
        _sourceFiles = sourceFiles ?? [];
    }

    public override void Execute(out string result)
    {
        EnumDB.Instance.Clear();

        if (_sourceFiles.Length == 0)
        {
            result = "[EnumCacheFromSourceCommand] SystemEnumFiles 가 비어 있습니다. 등록된 enum 이 없습니다.";
            Logger.Instance.Warning(result);
            return;
        }

        if (!EnumSourceParser.TryParse(_sourceFiles, out var files, out var errors))
        {
            foreach (var error in errors)
                Logger.Instance.Error($"[EnumCacheFromSourceCommand] {error}");
            EnumDB.Instance.Clear();
            throw new InvalidOperationException($"enum 소스 파싱 실패 ({errors.Count}건). EnumDB.bin 을 갱신하지 않습니다.");
        }

        foreach (var file in files)
        {
            if (file.Enums.Count == 0)
            {
                Logger.Instance.Warning($"[EnumCacheFromSourceCommand] enum 이 없는 파일입니다: {file.FilePath}");
                continue;
            }

            if (!EnumDB.Instance.Cache.LoadFromDefinitions(file.SheetName, file.Enums))
            {
                EnumDB.Instance.Clear();
                throw new InvalidOperationException($"enum 등록 실패: {file.FilePath}. EnumDB.bin 을 갱신하지 않습니다.");
            }

            Logger.Instance.Log($"[EnumCacheFromSourceCommand] 로드 완료: {file.FilePath} ({file.Enums.Count}개 Enum)");
        }

        result = $"[EnumCacheFromSourceCommand] EnumCache 생성 완료: {EnumDB.Instance.EnumCount}개 Enum 타입, {EnumDB.Instance.Cache.SheetCount}개 파일";
    }
}

/// <summary>
/// EnumDB 테스트 커맨드 (Development 모드에서만 동작)
/// EnumDB의 모든 데이터를 콘솔에 출력하고 txt 파일로 저장합니다.
/// </summary>
public class EnumDBTestCommand : Command
{
    private readonly string _filePath;

    public EnumDBTestCommand(string filePath)
    {
        _filePath = filePath;
    }

    public override void Execute(out string result)
    {
        result = "";

        try
        {
            var sb = new StringBuilder();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            sb.AppendLine("========================================");
            sb.AppendLine($"[EnumDB Test Report]");
            sb.AppendLine($"생성 시간: {timestamp}");
            sb.AppendLine("========================================");
            sb.AppendLine();

            if (EnumDB.Instance.IsEmpty)
            {
                sb.AppendLine("EnumDB가 비어있습니다.");
            }
            else
            {
                sb.AppendLine($"총 시트: {EnumDB.Instance.Cache.SheetCount}개");
                sb.AppendLine($"총 Enum 타입: {EnumDB.Instance.EnumCount}개");
                sb.AppendLine();

                foreach (var sheetName in EnumDB.Instance.Cache.SheetNames)
                {
                    sb.AppendLine($"[시트: {sheetName}]");
                    var enumTypes = EnumDB.Instance.GetEnumTypesBySheet(sheetName);
                    
                    foreach (var enumType in enumTypes)
                    {
                        var enumNamespace = EnumDB.Instance.GetEnumNamespace(enumType);
                        sb.AppendLine(string.IsNullOrEmpty(enumNamespace)
                            ? $"  └─ {enumType}"
                            : $"  └─ {enumType}  (namespace {enumNamespace})");
                        var members = EnumDB.Instance.GetEnumMembers(enumType);
                        
                        foreach (var (member, value) in members)
                        {
                            sb.AppendLine($"       {member} = {value}");
                        }
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("========================================");

            var content = sb.ToString();

            // 콘솔 출력
            Console.WriteLine(content);

            // 파일 저장
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, content);

            result = $"[EnumDBTestCommand] - 테스트 완료. 저장 경로: {Path.GetFullPath(_filePath)}";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[EnumDBTestCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// EnumDB 바이너리 파일을 로드하는 커맨드 (Development 모드에서만 동작)
/// 저장된 EnumDB.bin을 다시 로드하여 정상 저장 여부를 검증합니다.
/// </summary>
public class EnumDBLoadCommand : Command
{
    public override void Execute(out string result)
    {
        result = "";

        var enumDBPath = SystemPath.EnumBinaryPath;

        if (!File.Exists(enumDBPath))
        {
            result = $"[EnumDBLoadCommand] EnumDB 파일을 찾을 수 없습니다: {enumDBPath}";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            // 기존 캐시 클리어 후 바이너리에서 로드
            EnumDB.Instance.Clear();
            
            if (!EnumDB.Instance.LoadFromBinary(enumDBPath))
            {
                result = $"[EnumDBLoadCommand] EnumDB 로드 실패: {enumDBPath}";
                Logger.Instance.Error(result);
                return;
            }

            var enumCount = EnumDB.Instance.EnumCount;
            var sheetCount = EnumDB.Instance.Cache.SheetCount;
            result = $"[EnumDBLoadCommand] EnumDB 로드 완료: {enumDBPath} ({enumCount}개 Enum 타입, {sheetCount}개 시트)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[EnumDBLoadCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// EnumDB 바이너리 파일을 생성하는 커맨드
/// 시스템 통일 경로(SystemPath.EnumBinaryPath)에 저장합니다.
/// </summary>
public class GenerateEnumBinaryCommand : Command
{
    public override void Execute(out string result)
    {
        result = "";
        
        var enumDBPath = SystemPath.EnumBinaryPath;

        if (EnumDB.Instance.IsEmpty)
        {
            // 등록된 enum 이 없으면 이전 실행의 EnumDB.bin 이 남아 데이터 추출에 쓰이지 않도록 지운다.
            if (File.Exists(enumDBPath))
                File.Delete(enumDBPath);
            result = $"[GenerateEnumBinaryCommand] 등록된 enum 이 없어 EnumDB 를 만들지 않습니다: {enumDBPath}";
            Logger.Instance.Warning(result);
            return;
        }

        try
        {
            
            // 디렉토리 생성
            var directory = Path.GetDirectoryName(enumDBPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            EnumDB.Instance.SaveToBinary(enumDBPath);
            result = $"[GenerateEnumBinaryCommand] EnumDB 생성 완료: {enumDBPath} ({EnumDB.Instance.EnumCount}개 Enum)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[GenerateEnumBinaryCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

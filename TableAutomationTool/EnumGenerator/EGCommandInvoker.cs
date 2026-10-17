using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.Shared;

namespace TableAutomationTool.EnumGenerator;

/// <summary>
/// enum 단계: SystemEnumFiles 의 C# enum 소스 → EnumCache → Binary/EnumDB.bin
/// </summary>
public class EGCommandInvoker : CommandInvoker
{
    public EGCommandInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateInitConfigCommands()
    {
        var list = new List<ICommand>();
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.EnumGeneratorConfigPath}";

        list.Add(new CreateEnumGenerateConfigCommand(fullPath));
        return list;
    }

    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();

        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.EnumGeneratorConfigPath}";
        if (!File.Exists(fullPath))
        {
            // 설정 파일만 만들고 끝낸다. SystemEnumFiles 를 채운 뒤 다시 실행한다.
            list.Add(new CreateEnumGenerateConfigCommand(fullPath));
            return list;
        }

        if (!FileLoader.LoadAndConvertJson(fullPath, out EnumGeneratorConfig config))
            return list;

        // 1. EnumCache 생성 (enum 소스 파일 파싱)
        list.Add(new EnumCacheFromSourceCommand(config.SystemEnumFiles));

        // 2. [Dev] EnumDB 덤프 (바이너리 저장 전)
        var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
        if (SystemDefine.DevelopmentMode)
            list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, "EnumDB_BeforeSave.txt")));

        // 3. EnumDB 바이너리 저장
        list.Add(new GenerateEnumBinaryCommand());

        // 4. [Dev] 저장한 바이너리를 다시 읽어 검증
        if (SystemDefine.DevelopmentMode)
        {
            list.Add(new EnumDBLoadCommand());
            list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, "EnumDB_AfterSave.txt")));
        }

        return list;
    }
}

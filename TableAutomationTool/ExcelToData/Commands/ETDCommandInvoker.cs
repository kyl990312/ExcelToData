using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.Shared;

namespace TableAutomationTool.ExcelToData;

/// <summary>
/// ExcelToData 명령 실행자 - 구조화된 바이너리 지원 추가
/// </summary>
public class ETDCommandInvoker : CommandInvoker
{
    public ETDCommandInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateInitConfigCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;

        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemPath.BinaryConfigPath)));
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemPath.JsonConfigPath)));
        commands.Add(new GenerateGAuthConfigCommand(Path.Combine(baseDirectory, SystemPath.GAuthConfigPath)));

        return commands;
    }

    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;

        AddEnumDBCommand(ref commands);
        AddStructuredBinaryCommands(ref commands, baseDirectory);
        AddJsonCommands(ref commands, baseDirectory);

        return commands;
    }

    private void AddEnumDBCommand(ref List<ICommand> commands)
    {
        commands.Add(new LoadEnumDBCommand());
    }

    private void AddJsonCommands(ref List<ICommand> commands, string baseDirectory)
    {
        var jsonConfigPath = Path.Combine(baseDirectory, SystemPath.JsonConfigPath);

        if (!File.Exists(jsonConfigPath))
        {
            commands.Add(new GenerateConfigCommand(jsonConfigPath));
        }
        else
        {
            commands.Add(new ExtractJsonCommand(jsonConfigPath, _targetFileName));
        }
    }

    private void AddStructuredBinaryCommands(ref List<ICommand> commands, string baseDirectory)
    {
        var jsonConfigPath = Path.Combine(baseDirectory, SystemPath.BinaryConfigPath);

        if (!File.Exists(jsonConfigPath))
        {
            commands.Add(new GenerateConfigCommand(jsonConfigPath));
        }
        else
        {
            commands.Add(new ExtractBinaryCommand(jsonConfigPath, _targetFileName));
            commands.Add(new ExtractBinaryFromGSheet(jsonConfigPath, _targetFileName));
        }
    }
}

/// <summary>
/// 테스트용 명령 실행자 - 모든 설정 파일 생성 실행
/// </summary>
public class ETDCommandTestInvoker : CommandInvoker
{
    public ETDCommandTestInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;

        // 모든 설정 파일 생성
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemPath.BinaryConfigPath)));
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemPath.JsonConfigPath)));

        return commands;
    }
}

/// <summary>
/// 구조화된 바이너리만 처리하는 명령 실행자
/// </summary>
public class StructuredBinaryOnlyInvoker : CommandInvoker
{
    public StructuredBinaryOnlyInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        var configPath = Path.Combine(baseDirectory, SystemPath.BinaryConfigPath);

        if (!File.Exists(configPath))
        {
            commands.Add(new GenerateConfigCommand(configPath));
        }
        else
        {
            commands.Add(new ExtractBinaryCommand(configPath, _targetFileName));
        }

        return commands;
    }
}

using Common.Command;

namespace ExcelToData;

/// <summary>
/// ExcelToData 명령어 실행기 - 구조화된 바이너리 지원 추가
/// </summary>
public class ETDCommandInvoker : CommandInvoker
{
    public ETDCommandInvoker() : base()
    {
    }

    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        
        AddStructuredBinaryCommands(commands, baseDirectory);
        AddJsonCommands(commands, baseDirectory);
        
        return commands;
    }

    private void AddBinaryCommands(List<ICommand> commands, string baseDirectory)
    {
        var binaryConfigPath = Path.Combine(baseDirectory, SystemDefine.BinaryConfigPath);
        
        if (!File.Exists(binaryConfigPath))
        {
            commands.Add(new GenerateConfigCommand(binaryConfigPath));
        }
        else
        {
            commands.Add(new ExtractBinaryCommand(binaryConfigPath));
        }
    }

    private void AddJsonCommands(List<ICommand> commands, string baseDirectory)
    {
        var jsonConfigPath = Path.Combine(baseDirectory, SystemDefine.JsonConfigPath);
        
        if (!File.Exists(jsonConfigPath))
        {
            commands.Add(new GenerateConfigCommand(jsonConfigPath));
        }
        else
        {
            commands.Add(new ExtractJsonCommand(jsonConfigPath));
        }
    }

    private void AddStructuredBinaryCommands(List<ICommand> commands, string baseDirectory)
    {
        var jsonConfigPath = Path.Combine(baseDirectory, SystemDefine.StructuredBinaryConfigPath);
        
        if (!File.Exists(jsonConfigPath))
        {
            commands.Add(new GenerateConfigCommand(jsonConfigPath));
        }
        else
        {
            commands.Add(new ExtractStructuredBinaryCommand(jsonConfigPath));
        }
    }
}

/// <summary>
/// 테스트용 명령어 실행기 - 모든 설정 파일 생성만 수행
/// </summary>
public class ETDCommandTestInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        
        // 모든 설정 파일 생성
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemDefine.BinaryConfigPath)));
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemDefine.JsonConfigPath)));
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemDefine.StructuredBinaryConfigPath)));
        
        return commands;
    }
}

/// <summary>
/// 구조화된 바이너리만 처리하는 명령어 실행기
/// </summary>
public class StructuredBinaryOnlyInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        var configPath = Path.Combine(baseDirectory, SystemDefine.StructuredBinaryConfigPath);
        
        if (!File.Exists(configPath))
        {
            commands.Add(new GenerateConfigCommand(configPath));
        }
        else
        {
            commands.Add(new ExtractStructuredBinaryCommand(configPath));
        }
        
        return commands;
    }
}

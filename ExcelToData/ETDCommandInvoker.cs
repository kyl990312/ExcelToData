using Common.Command;
using ExcelToData.ExtractBytes;
namespace ExcelToData;


public class ETDCommandInvoker : CommandInvoker
{
    public ETDCommandInvoker() : base()
    {
    }

    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        
        if(!File.Exists(SystemDefine.BinaryConfigPath))
            commands.Add(new GenerateConfigCommand($"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemDefine.BinaryConfigPath}"));
        else
            commands.Add(new ExtractBinaryCommand($"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemDefine.BinaryConfigPath}"));
        
        if(!File.Exists(SystemDefine.JsonConfigPath))
            commands.Add(new GenerateConfigCommand($"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemDefine.JsonConfigPath}"));
        else
            commands.Add(new ExtractJsonCommand($"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemDefine.JsonConfigPath}"));
        
        return commands;
    }
}

public class ETDCommandTestInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        
        commands.Add(new GenerateConfigCommand($"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemDefine.BinaryConfigPath}"));
        commands.Add(new GenerateConfigCommand($"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemDefine.JsonConfigPath}"));
        
        return commands;
    }
}
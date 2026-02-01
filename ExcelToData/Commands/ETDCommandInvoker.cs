using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomarionToolShared;

namespace ExcelToData;

/// <summary>
/// ExcelToData ëª…ë ¹???¤í–‰ê¸?- êµ¬ì¡°?”ëœ ë°”ì´?ˆë¦¬ ì§€??ì¶”ê?
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
            commands.Add(new ExtractJsonCommand(jsonConfigPath));
        }
    }

    private void AddStructuredBinaryCommands(ref List<ICommand> commands, string baseDirectory)
    {
        var jsonConfigPath = Path.Combine(baseDirectory, ProjectDefine.StructuredBinaryConfigPath);
        
        if (!File.Exists(jsonConfigPath))
        {
            commands.Add(new GenerateConfigCommand(jsonConfigPath));
        }
        else
        {
            commands.Add(new ExtractBinaryCommand(jsonConfigPath));
            commands.Add(new ExtractBinaryFromGSheet(jsonConfigPath));
        }
    }
}

/// <summary>
/// ?ŒìŠ¤?¸ìš© ëª…ë ¹???¤í–‰ê¸?- ëª¨ë“  ?¤ì • ?Œì¼ ?ì„±ë§??˜í–‰
/// </summary>
public class ETDCommandTestInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        
        // ëª¨ë“  ?¤ì • ?Œì¼ ?ì„±
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemPath.BinaryConfigPath)));
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, SystemPath.JsonConfigPath)));
        commands.Add(new GenerateConfigCommand(Path.Combine(baseDirectory, ProjectDefine.StructuredBinaryConfigPath)));
        
        return commands;
    }
}

/// <summary>
/// êµ¬ì¡°?”ëœ ë°”ì´?ˆë¦¬ë§?ì²˜ë¦¬?˜ëŠ” ëª…ë ¹???¤í–‰ê¸?/// </summary>
public class StructuredBinaryOnlyInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var commands = new List<ICommand>();
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        var configPath = Path.Combine(baseDirectory, ProjectDefine.StructuredBinaryConfigPath);
        
        if (!File.Exists(configPath))
        {
            commands.Add(new GenerateConfigCommand(configPath));
        }
        else
        {
            commands.Add(new ExtractBinaryCommand(configPath));
        }
        
        return commands;
    }
}

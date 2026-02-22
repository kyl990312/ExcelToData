namespace TableAutomation.Common.Command;

public abstract class CommandInvoker
{
    protected List<ICommand> _commands = new();
    protected string? _targetFileName = null;

    public CommandInvoker(string? targetFileName = null)
    {
        _targetFileName = targetFileName;
        _commands = CreateCommands();
    }

    public void ExecuteAll()
    {
        if (_commands == null)
            return;

        foreach (ICommand command in _commands)
        {
            command.Execute(out string result);
            command.LogResult(result);
        }
    }

    protected abstract List<ICommand> CreateCommands();
}
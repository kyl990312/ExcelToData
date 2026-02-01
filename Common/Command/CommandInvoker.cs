namespace TableAutomation.Common.Command;

public abstract class CommandInvoker
{
    protected List<ICommand> _commands = new();

    public CommandInvoker()
    {
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
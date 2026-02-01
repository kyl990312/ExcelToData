using TableAutomation.Common.Utils;

namespace TableAutomation.Common.Command;

public interface ICommand
{
    void Execute(out string resultMsg);
    void LogResult(string msg);
}

public abstract class Command : ICommand
{
    public abstract void Execute(out string result);

    public void LogResult(string msg)
    {
        Logger.Instance.Log(msg);
    }
}

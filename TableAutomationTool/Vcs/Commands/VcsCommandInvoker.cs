using TableAutomationTool.Common.Command;
using TableAutomationTool.Shared;

namespace TableAutomationTool.Vcs;

/// <summary>
/// VCS(diff) 관련 명령 실행자. init-config 로 GitConfig.json 을 만든다.
/// </summary>
public class VcsCommandInvoker : CommandInvoker
{
    public VcsCommandInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateInitConfigCommands()
    {
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;

        return new List<ICommand>
        {
            new GenerateGitConfigCommand(Path.Combine(baseDirectory, SystemPath.GitConfigPath))
        };
    }

    protected override List<ICommand> CreateCommands()
    {
        return new List<ICommand>();
    }
}

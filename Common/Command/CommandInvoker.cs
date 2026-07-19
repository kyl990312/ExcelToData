namespace TableAutomation.Common.Command;

public abstract class CommandInvoker
{
    protected List<ICommand> _commands;
    protected string[] _args;
    protected string? _targetFileName;

    public CommandInvoker(string[] args)
    {
        _args = args;
        ParseArgs();
        _commands = BuildCommands();
    }

    public void ExecuteAll()
    {

        foreach (ICommand command in _commands)
        {
            command.Execute(out string result);
            command.LogResult(result);
        }
    }

    /// <summary>
    /// args를 파싱하여 _targetFileName 등 공통 필드를 설정
    /// </summary>
    private void ParseArgs()
    {
        if (_args.Length >= 2 && _args[0] == "-each")
        {
            _targetFileName = _args[1];
        }
    }

    /// <summary>
    /// args에 따라 적절한 커맨드 리스트를 구성
    /// </summary>
    private List<ICommand> BuildCommands()
    {
        if (_args.Length >= 1 && _args[0].ToLower() == "init-config")
        {
            return CreateInitConfigCommands();
        }

        return CreateCommands();
    }

    /// <summary>
    /// init-config 명령어 시 실행할 Config 생성 커맨드 목록
    /// </summary>
    protected virtual List<ICommand> CreateInitConfigCommands()
    {
        return new List<ICommand>();
    }

    /// <summary>
    /// 기본 실행 커맨드 목록
    /// </summary>
    protected abstract List<ICommand> CreateCommands();
}
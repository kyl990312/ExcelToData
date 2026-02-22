using CodeGenerator;
using TableAutomation.Common.Utils;

Logger.Instance.Log("[CodeGenerator] Start Generate Code");

// 커맨드 라인 인자 파싱
string? targetFileName = null;
if (args.Length >= 2 && args[0] == "-each")
{
    targetFileName = args[1];
    Logger.Instance.Log($"[CodeGenerator] Target file specified: {targetFileName}");
}

var commandInvoker = new CGCommandInvoker(targetFileName);
commandInvoker.ExecuteAll();
Logger.Instance.Log("[CodeGenerator] Complete Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}

using CodeGenerator;
using TableAutomation.Common.Utils;

Logger.Instance.Log("[CodeGenerator] Start Generate Code");
var commandInvoker = new CGCommandInvoker();
commandInvoker.ExecuteAll();
Logger.Instance.Log("[CodeGenerator] Complete Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}

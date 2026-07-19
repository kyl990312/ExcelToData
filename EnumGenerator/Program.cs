using TableAutomation.Common.Utils;
using EnumGenerator;

Logger.Instance.Log("[EnumGenerator] Start");

var commandInvoker = new EGCommandInvoker(args);
commandInvoker.ExecuteAll();

Logger.Instance.Log("[EnumGenerator] Quit Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
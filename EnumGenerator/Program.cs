using Common.Utils;
using EnumGenerator;

Logger.Instance.Log("[ExcelToData] Start Extract Data");
var commandInvoker = new EGCommandInvoker();
commandInvoker.ExecuteAll();
Logger.Instance.Log("[ExcelToData] Quit Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
using ExcelToData;
using TableAutomation.Common.Utils;

Logger.Instance.Log("[ExcelToData] Start Extract Data");

var commandInvoker = new ETDCommandInvoker(args);
commandInvoker.ExecuteAll();

Logger.Instance.Log("[ExcelToData] Quit Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}




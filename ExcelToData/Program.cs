using ExcelToData;
using Common.Utils;
using ExcelToData.ExtractBytes;

Logger.Instance.Log("[ExcelToData] Start Extract Data");
var commandInvoker = new ETDCommandInvoker();
commandInvoker.ExecuteAll();
Logger.Instance.Log("[ExcelToData] Quit Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}




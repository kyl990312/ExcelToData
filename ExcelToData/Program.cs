using ExcelToData;
using TableAutomation.Common.Utils;
using ExcelToData.ExtractBytes;

Logger.Instance.Log("[ExcelToData] Start Extract Data");

// 커맨드 라인 인자 파싱
string? targetFileName = null;
if (args.Length >= 2 && args[0] == "-each")
{
    targetFileName = args[1];
    Logger.Instance.Log($"[ExcelToData] Target file specified: {targetFileName}");
}

var commandInvoker = new ETDCommandInvoker(targetFileName);
commandInvoker.ExecuteAll();
Logger.Instance.Log("[ExcelToData] Quit Program");
if (Environment.UserInteractive)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}




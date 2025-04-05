using Common.Utils;
using EnumGenerator;

Logger.Instance.Log("[ExcelToData] Start Extract Data");
var commandInvoker = new EGCommandInvoker();
commandInvoker.ExecuteAll();
Logger.Instance.Log("[ExcelToData] Quit Program");
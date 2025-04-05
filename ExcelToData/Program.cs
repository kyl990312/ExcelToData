using ExcelToData;
using Common.Utils;
using ExcelToData.ExtractBytes;

Logger.Instance.Log("[ExcelToData] Start Extract Data");
var commandInvoker = new ETDCommandInvoker();
commandInvoker.ExecuteAll();
Logger.Instance.Log("[ExcelToData] Quit Program");




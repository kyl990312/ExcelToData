using CodeGenerator;
using Common.Utils;

Logger.Instance.Log("[CodeGenerator] Start Generate Code");
var commandInvoker = new CGCommandInvoker();
commandInvoker.ExecuteAll();
Logger.Instance.Log("[CodeGenerator] Quit Program");

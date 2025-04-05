using Common.Command;
using Common.Utils;
using ClosedXML.Excel;
using Exception = System.Exception;

namespace EnumGenerator;

public class CreateEnumGenerateConfigCommand : Command
{
    private string _path;
    public CreateEnumGenerateConfigCommand(string path)
    {
        _path = path;
    }
    
    public override void Execute(out string resultMsg)
    {
        resultMsg = string.Empty;
        var config = EnumGeneratorConfig.GetDefault();
        if (FileLoader.SaveJson(_path, config))
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Success Craete Config : {_path}";
        }
        else
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Fail to Craete Config : {_path}";
        }
    }
}

public class GenerateEnumCommand : Command
{
    private string _enumFilePath;
    private string _directoryPath;
    private string _codeExtension;

    public GenerateEnumCommand(string enumFilePath, string directoryPath, string codeExtension)
    {
        _enumFilePath = enumFilePath;
        _directoryPath = directoryPath;
        _codeExtension = codeExtension;
    }
    
    public override void Execute(out string result)
    {
        result = "";
        try
        {
            var enumGenerator = new Generator.EnumGenerator.EnumGenerator();
            using (var workbook = new XLWorkbook(_enumFilePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    enumGenerator.Generate(worksheet, _directoryPath, _codeExtension);
                    var enums = enumGenerator.CreateEnumList();
                    Logger.Instance.Log($"[GenerateEnumCommand] Create Enums from {_enumFilePath}>{worksheet.Name} : {enums.ToString()}");
                }
            }
            result = $"[GenerateEnumCommand] Complete Create Enums {_enumFilePath}";
        }
        catch (Exception e)
        {
            Logger.Instance.Log($"[GenerateEnumCommand] Failed Execute Command\n{e.Message}");
        }
    }
}
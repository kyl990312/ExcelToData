using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.Shared;

namespace TableAutomationTool.CodeGenerator;

public class CGCommandInvoker : CommandInvoker
{
    public CGCommandInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateInitConfigCommands()
    {
        var list = new List<ICommand>();
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.CodeGeneratorConfigPath}";

        list.Add(new GenerateCodeConfigCommand(fullPath));
        return list;
    }

    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.CodeGeneratorConfigPath}";

        if(!File.Exists(fullPath))
        {
            list.Add(new GenerateCodeConfigCommand(fullPath));
            return list;
        }

        if (!FileLoader.LoadAndConvertJson(fullPath, out CodeGenerateConfig config))
        {
            return list;
        }

        // EnumDB 로드
        list.Add(new LoadEnumDBCommand());

        // 클래스 코드 생성
        if (ProjectDefine.CodeExtension.EndsWith("cs"))
        {
            list.Add(new GenerateCSharpCodeCommand(config, _targetFileName));
        }
        else if(ProjectDefine.CodeExtension.EndsWith("cpp"))
        {
            list.Add(new GenerateCppCodeCommand(config, _targetFileName));
        }

        return list;
    }
}

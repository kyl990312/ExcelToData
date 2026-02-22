using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomarionToolShared;

namespace CodeGenerator;

public class CGCommandInvoker : CommandInvoker
{
    public CGCommandInvoker(string? targetFileName = null) : base(targetFileName)
    {
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

        // Enum 코드 생성
        if (config.IsGenerateEnumCode)
        {
            list.Add(new GenerateEnumCodeCommand(config.EnumOutputDirectory, config.CodeExtension));
        }

        return list;
    }
}

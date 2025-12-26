using Common;
using Common.Command;
using TableAutomarionToolShared;

namespace CodeGenerator;

public class CGCommandInvoker : CommandInvoker
{
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

        list.Add(new LoadEnumDBCommand());

        if (ProjectDefine.CodeExtension.EndsWith("cs"))
        {
            list.Add(new GenerateCSharpCodeCommand(config));
        }
        
        return list;
    }
}

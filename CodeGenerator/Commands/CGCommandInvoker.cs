using Common.Command;
using Generator.CodeGenerator;

namespace CodeGenerator;

public class CGCommandInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{ProjectDefine.CodeGenerateConfigPath}";
        
        if(!File.Exists(fullPath))
        {
            list.Add(new GenerateCodeConfigCommand(fullPath));
            return list;
        }
        
        if (!FileLoader.LoadAndConvertJson(fullPath, out CodeGenerateConfig config))
        {
            return list;
        }
        
        if (ProjectDefine.CodeExtension.EndsWith("cs"))
        {
            list.Add(new GenerateCSharpCodeCommand(config));
            // 구조화된 바이너리 지원 추가
            list.Add(new GenerateStructuredBinaryCSharpCommand(fullPath));
        }
        
        return list;
    }
}

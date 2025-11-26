using Common.Command;

namespace EnumGenerator;

public class EGCommandInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{ProjectDefine.ConfigPath}";
        if(!File.Exists(fullPath))
            list.Add(new CreateEnumGenerateConfigCommand(fullPath));

        if (!FileLoader.LoadAndConvertJson(fullPath, out EnumGeneratorConfig config))
            return list;
        
        // Enum 코드 생성
        list.Add(new GenerateEnumCodeCommand(config.EnumFilePath, config.DstDirectory, config.CodeExtension));
        
        // EnumDB 바이너리 생성
        list.Add(new GenerateEnumDBCommand(config.EnumFilePath, config.EnumDBPath));
        
        return list;
    }
}

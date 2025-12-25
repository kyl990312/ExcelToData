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
        
        // 1. EnumCache 생성 (최우선 실행)
        if (config.IsLoadFromGSheet && !string.IsNullOrEmpty(config.SrcGDriveLink))
        {
            // 구글 시트에서 로드
            list.Add(new EnumCacheGenerateFromGSheetCommand(config.SrcGDriveLink, config.EnumSheetPatterns));
        }
        else if (config.IsLoadFromLocal && !string.IsNullOrEmpty(config.EnumFilePath))
        {
            // 로컬 파일에서 로드
            list.Add(new EnumCacheGenerateCommand(config.EnumFilePath));
        }
        
        // 2. Enum 코드 생성 (EnumCache 기반)
        list.Add(new GenerateEnumCodeCommand(config.DstDirectory, config.CodeExtension));
        
        // 3. EnumDB 바이너리 생성
        if (config.IsGenerateEnumBinary)
        {
            list.Add(new GenerateEnumBinaryCommand(config.EnumDBPath));
        }
        
        return list;
    }
}

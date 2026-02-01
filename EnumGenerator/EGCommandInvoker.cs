using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomarionToolShared;

namespace EnumGenerator;

public class EGCommandInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.EnumGeneratorConfigPath}";
        if(!File.Exists(fullPath))
            list.Add(new CreateEnumGenerateConfigCommand(fullPath));

        if (!FileLoader.LoadAndConvertJson(fullPath, out EnumGeneratorConfig config))
            return list;
        
        // 1. EnumCache ?�성 (최우???�행)
        if (config.IsLoadFromGSheet && !string.IsNullOrEmpty(config.SrcGDriveLink))
        {
            // 구�? ?�트?�서 로드
            list.Add(new EnumCacheGenerateFromGSheetCommand(config.SrcGDriveLink, config.EnumSheetPatterns));
        }
        else if (config.IsLoadFromLocal && !string.IsNullOrEmpty(config.EnumFilePath))
        {
            // 로컬 ?�일?�서 로드
            list.Add(new EnumCacheGenerateCommand(config.EnumFilePath));
        }

        // 2. [Dev] EnumDB ?�스??(바이?�리 ?�????
        {
            var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
            if (SystemDefine.DevelopmentMode)
                list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, $"EnumDB_BeforeSave.txt")));
        }

        // 3. EnumDB 바이?�리 ?�성
        list.Add(new GenerateEnumBinaryCommand());

        // 4. [Dev] EnumDB 로드 (?�?�된 바이?�리 검�?
        if (SystemDefine.DevelopmentMode)
            list.Add(new EnumDBLoadCommand());

        // 5. [Dev] EnumDB ?�스??(바이?�리 로드 ??
        {
            var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
            if (SystemDefine.DevelopmentMode)
                list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, $"EnumDB_AfterSave.txt")));
        }

        return list;
    }
}

using Common;
using Common.Command;
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

        // 2. [Dev] EnumDB 테스트 (바이너리 저장 전)
        {
            var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
            if (SystemDefine.DevelopmentMode)
                list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, $"EnumDB_BeforeSave.txt")));
        }

        // 3. EnumDB 바이너리 생성
        list.Add(new GenerateEnumBinaryCommand());

        // 4. [Dev] EnumDB 로드 (저장된 바이너리 검증)
        if (SystemDefine.DevelopmentMode)
            list.Add(new EnumDBLoadCommand());

        // 5. [Dev] EnumDB 테스트 (바이너리 로드 후)
        {
            var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
            if (SystemDefine.DevelopmentMode)
                list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, $"EnumDB_AfterSave.txt")));
        }

        return list;
    }
}

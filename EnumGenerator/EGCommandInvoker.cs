using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomarionToolShared;

namespace EnumGenerator;

public class EGCommandInvoker : CommandInvoker
{
    public EGCommandInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateInitConfigCommands()
    {
        var list = new List<ICommand>();
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.EnumGeneratorConfigPath}";

        list.Add(new CreateEnumGenerateConfigCommand(fullPath));
        return list;
    }

    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        
        var fullPath =
            $"{Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)}/{SystemPath.EnumGeneratorConfigPath}";
        if(!File.Exists(fullPath))
            list.Add(new CreateEnumGenerateConfigCommand(fullPath));

        if (!FileLoader.LoadAndConvertJson(fullPath, out EnumGeneratorConfig config))
            return list;
        
        // 1. EnumCache ���� (�ֿ켱 ����)
        if (config.IsLoadFromGSheet && !string.IsNullOrEmpty(config.SrcGDriveLink))
        {
            // ���� ��Ʈ���� �ε�
            list.Add(new EnumCacheGenerateFromGSheetCommand(config.SrcGDriveLink, config.EnumSheetPatterns));
        }
        else if (config.IsLoadFromLocal && !string.IsNullOrEmpty(config.EnumFilePath))
        {
            // ���� ���Ͽ��� �ε�
            list.Add(new EnumCacheGenerateCommand(config.EnumFilePath));
        }

        // 2. [Dev] EnumDB �׽�Ʈ (���̳ʸ� ���� ��)
        {
            var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
            if (SystemDefine.DevelopmentMode)
                list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, $"EnumDB_BeforeSave.txt")));
        }

        // 3. EnumDB ���̳ʸ� ����
        list.Add(new GenerateEnumBinaryCommand());

        // 4. [Dev] EnumDB �ε� (����� ���̳ʸ� ����)
        if (SystemDefine.DevelopmentMode)
            list.Add(new EnumDBLoadCommand());

        // 5. [Dev] EnumDB �׽�Ʈ (���̳ʸ� �ε� ��)
        {
            var enumFileName = Path.GetFileName(SystemPath.EnumBinaryPath);
            if (SystemDefine.DevelopmentMode)
                list.Add(new EnumDBTestCommand(SystemPath.EnumBinaryPath.Replace(enumFileName, $"EnumDB_AfterSave.txt")));
        }

        return list;
    }
}

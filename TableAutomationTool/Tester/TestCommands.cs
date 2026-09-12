using TableAutomationTool.Common;
using TableAutomationTool.Common.Command;
using TableAutomationTool.Common.Utils;
using TableAutomationTool.UserEnum;
using TableAutomationTool.Shared;

namespace TableAutomationTool.Tester;

public class TestCommandInvoker : CommandInvoker
{
    public TestCommandInvoker(string[] args) : base(args)
    {
    }

    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        list.Add(new TestEnumDBSharedMemoryCommand());
        return list;
    }
}

/// <summary>
/// EnumDB ���� �޸� ��� �׽�Ʈ Ŀ�ǵ�
/// 1. ���� EnumDB ���̳ʸ� ���� �ε�
/// 2. ���� �޸𸮿� ����
/// 3. ĳ�� Ŭ����
/// 4. ���� �޸𸮿��� �б�
/// 5. ������ ����
/// </summary>
public class TestEnumDBSharedMemoryCommand : Command
{
    public override void Execute(out string result)
    {
        result = "";
        
        try
        {
            Logger.Instance.Log("========================================");
            Logger.Instance.Log("[TestEnumDBSharedMemory] �׽�Ʈ ����");
            Logger.Instance.Log("========================================");
            
            var enumDBPath = SystemPath.EnumBinaryPath;
            
            // 1. ���� EnumDB ���̳ʸ� ���� �ε�
            Logger.Instance.Log("[Step 1] EnumDB ���̳ʸ� ���� �ε�");
            
            if (!File.Exists(enumDBPath))
            {
                result = $"[TestEnumDBSharedMemory] EnumDB ������ ã�� �� �����ϴ�: {enumDBPath}";
                Logger.Instance.Error(result);
                return;
            }
            
            EnumDB.Instance.Clear();
            if (!EnumDB.Instance.LoadFromBinary(enumDBPath))
            {
                result = $"[TestEnumDBSharedMemory] EnumDB �ε� ����: {enumDBPath}";
                Logger.Instance.Error(result);
                return;
            }
            
            var originalEnumCount = EnumDB.Instance.EnumCount;
            var originalEnumTypes = EnumDB.Instance.EnumTypes.ToList();
            var originalData = CaptureEnumData();
            
            Logger.Instance.Log($"  - �ε�� Enum Ÿ��: {originalEnumCount}��");
            foreach (var enumType in originalEnumTypes)
            {
                var members = EnumDB.Instance.GetEnumMembers(enumType);
                Logger.Instance.Log($"    * {enumType}: {members.Count}�� ���");
            }
            
            // 2. ���� �޸𸮿� ����
            Logger.Instance.Log("[Step 2] ���� �޸𸮿� ����");
            if (!EnumDB.Instance.WriteToSharedMemory())
            {
                result = "[TestEnumDBSharedMemory] ���� �޸� ���� ����";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - ���� �޸� ���� ����");
            
            // 3. ���� �޸� ���� Ȯ��
            Logger.Instance.Log("[Step 3] ���� �޸� ���� Ȯ��");
            if (!EnumDB.Instance.SharedMemoryExists())
            {
                result = "[TestEnumDBSharedMemory] ���� �޸𸮰� �������� ����";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - ���� �޸� ���� Ȯ�ε�");
            
            // 4. ĳ�� Ŭ����
            Logger.Instance.Log("[Step 4] EnumDB ĳ�� Ŭ����");
            EnumDB.Instance.Clear();
            
            if (!EnumDB.Instance.IsEmpty)
            {
                result = "[TestEnumDBSharedMemory] ĳ�� Ŭ���� ����";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - ĳ�� Ŭ���� �Ϸ�");
            
            // 5. ���� �޸𸮿��� �б�
            Logger.Instance.Log("[Step 5] ���� �޸𸮿��� �б�");
            if (!EnumDB.Instance.ReadFromSharedMemory())
            {
                result = "[TestEnumDBSharedMemory] ���� �޸� �б� ����";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - ���� �޸� �б� ����");
            
            // 6. ������ ����
            Logger.Instance.Log("[Step 6] ������ ����");
            var loadedEnumCount = EnumDB.Instance.EnumCount;
            
            if (loadedEnumCount != originalEnumCount)
            {
                result = $"[TestEnumDBSharedMemory] Enum ���� ����ġ: ����={originalEnumCount}, �ε�={loadedEnumCount}";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log($"  - Enum Ÿ�� ���� ��ġ: {loadedEnumCount}��");
            
            // �� Enum Ÿ�Ժ� ��� ����
            bool allMatch = true;
            foreach (var (enumType, originalMembers) in originalData)
            {
                if (!EnumDB.Instance.HasEnum(enumType))
                {
                    Logger.Instance.Error($"  - Enum Ÿ�� ����: {enumType}");
                    allMatch = false;
                    continue;
                }
                
                var loadedMembers = EnumDB.Instance.GetEnumMembers(enumType);
                
                if (loadedMembers.Count != originalMembers.Count)
                {
                    Logger.Instance.Error($"  - {enumType} ��� ���� ����ġ: ����={originalMembers.Count}, �ε�={loadedMembers.Count}");
                    allMatch = false;
                    continue;
                }
                
                // ����� �� ����
                foreach (var (member, value) in originalMembers)
                {
                    var loadedMember = loadedMembers.Find(m => m.member == member);
                    if (loadedMember.member == null || loadedMember.value != value)
                    {
                        Logger.Instance.Error($"  - {enumType}.{member} �� ����ġ: ����={value}, �ε�={loadedMember.value}");
                        allMatch = false;
                    }
                }
                
                Logger.Instance.Log($"  - {enumType}: {loadedMembers.Count}�� ��� ���� �Ϸ�");
            }
            
            if (!allMatch)
            {
                result = "[TestEnumDBSharedMemory] ������ ���� ����";
                Logger.Instance.Error(result);
                return;
            }
            
            // 7. LoadWithFallback �׽�Ʈ
            Logger.Instance.Log("[Step 7] LoadWithFallback �׽�Ʈ");
            EnumDB.Instance.Clear();
            
            // ���� �޸𸮿��� �ε� (�̹� �����ϹǷ� �����ؾ� ��)
            if (!EnumDB.Instance.LoadWithFallback(enumDBPath))
            {
                result = "[TestEnumDBSharedMemory] LoadWithFallback ����";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log($"  - LoadWithFallback ����: {EnumDB.Instance.EnumCount}�� Enum �ε��");
            
            Logger.Instance.Log("========================================");
            Logger.Instance.Log("[TestEnumDBSharedMemory] ��� �׽�Ʈ ���!");
            Logger.Instance.Log("========================================");
            
            result = "[TestEnumDBSharedMemory] �׽�Ʈ ����";
        }
        catch (Exception e)
        {
            result = $"[TestEnumDBSharedMemory] �׽�Ʈ ����: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
    
    /// <summary>
    /// ���� EnumDB �����͸� ĸó�մϴ�.
    /// </summary>
    private Dictionary<string, List<(string member, int value)>> CaptureEnumData()
    {
        var data = new Dictionary<string, List<(string member, int value)>>();
        
        foreach (var enumType in EnumDB.Instance.EnumTypes)
        {
            var members = EnumDB.Instance.GetEnumMembers(enumType);
            data[enumType] = new List<(string member, int value)>(members);
        }
        
        return data;
    }
}

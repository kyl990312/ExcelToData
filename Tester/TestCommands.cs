using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomation.Common.Utils;
using UserEnum;
using TableAutomarionToolShared;

public class TestCommandInvoker : CommandInvoker
{
    protected override List<ICommand> CreateCommands()
    {
        var list = new List<ICommand>();
        list.Add(new TestEnumDBSharedMemoryCommand());
        return list;
    }
}

/// <summary>
/// EnumDB 공유 메모리 기능 테스트 커맨드
/// 1. 기존 EnumDB 바이너리 파일 로드
/// 2. 공유 메모리에 쓰기
/// 3. 캐시 클리어
/// 4. 공유 메모리에서 읽기
/// 5. 데이터 검증
/// </summary>
public class TestEnumDBSharedMemoryCommand : Command
{
    public override void Execute(out string result)
    {
        result = "";
        
        try
        {
            Logger.Instance.Log("========================================");
            Logger.Instance.Log("[TestEnumDBSharedMemory] 테스트 시작");
            Logger.Instance.Log("========================================");
            
            var enumDBPath = SystemPath.EnumBinaryPath;
            
            // 1. 기존 EnumDB 바이너리 파일 로드
            Logger.Instance.Log("[Step 1] EnumDB 바이너리 파일 로드");
            
            if (!File.Exists(enumDBPath))
            {
                result = $"[TestEnumDBSharedMemory] EnumDB 파일을 찾을 수 없습니다: {enumDBPath}";
                Logger.Instance.Error(result);
                return;
            }
            
            EnumDB.Instance.Clear();
            if (!EnumDB.Instance.LoadFromBinary(enumDBPath))
            {
                result = $"[TestEnumDBSharedMemory] EnumDB 로드 실패: {enumDBPath}";
                Logger.Instance.Error(result);
                return;
            }
            
            var originalEnumCount = EnumDB.Instance.EnumCount;
            var originalEnumTypes = EnumDB.Instance.EnumTypes.ToList();
            var originalData = CaptureEnumData();
            
            Logger.Instance.Log($"  - 로드된 Enum 타입: {originalEnumCount}개");
            foreach (var enumType in originalEnumTypes)
            {
                var members = EnumDB.Instance.GetEnumMembers(enumType);
                Logger.Instance.Log($"    * {enumType}: {members.Count}개 멤버");
            }
            
            // 2. 공유 메모리에 쓰기
            Logger.Instance.Log("[Step 2] 공유 메모리에 쓰기");
            if (!EnumDB.Instance.WriteToSharedMemory())
            {
                result = "[TestEnumDBSharedMemory] 공유 메모리 쓰기 실패";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - 공유 메모리 쓰기 성공");
            
            // 3. 공유 메모리 존재 확인
            Logger.Instance.Log("[Step 3] 공유 메모리 존재 확인");
            if (!EnumDB.Instance.SharedMemoryExists())
            {
                result = "[TestEnumDBSharedMemory] 공유 메모리가 존재하지 않음";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - 공유 메모리 존재 확인됨");
            
            // 4. 캐시 클리어
            Logger.Instance.Log("[Step 4] EnumDB 캐시 클리어");
            EnumDB.Instance.Clear();
            
            if (!EnumDB.Instance.IsEmpty)
            {
                result = "[TestEnumDBSharedMemory] 캐시 클리어 실패";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - 캐시 클리어 완료");
            
            // 5. 공유 메모리에서 읽기
            Logger.Instance.Log("[Step 5] 공유 메모리에서 읽기");
            if (!EnumDB.Instance.ReadFromSharedMemory())
            {
                result = "[TestEnumDBSharedMemory] 공유 메모리 읽기 실패";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log("  - 공유 메모리 읽기 성공");
            
            // 6. 데이터 검증
            Logger.Instance.Log("[Step 6] 데이터 검증");
            var loadedEnumCount = EnumDB.Instance.EnumCount;
            
            if (loadedEnumCount != originalEnumCount)
            {
                result = $"[TestEnumDBSharedMemory] Enum 개수 불일치: 원본={originalEnumCount}, 로드={loadedEnumCount}";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log($"  - Enum 타입 개수 일치: {loadedEnumCount}개");
            
            // 각 Enum 타입별 멤버 검증
            bool allMatch = true;
            foreach (var (enumType, originalMembers) in originalData)
            {
                if (!EnumDB.Instance.HasEnum(enumType))
                {
                    Logger.Instance.Error($"  - Enum 타입 누락: {enumType}");
                    allMatch = false;
                    continue;
                }
                
                var loadedMembers = EnumDB.Instance.GetEnumMembers(enumType);
                
                if (loadedMembers.Count != originalMembers.Count)
                {
                    Logger.Instance.Error($"  - {enumType} 멤버 개수 불일치: 원본={originalMembers.Count}, 로드={loadedMembers.Count}");
                    allMatch = false;
                    continue;
                }
                
                // 멤버별 값 검증
                foreach (var (member, value) in originalMembers)
                {
                    var loadedMember = loadedMembers.Find(m => m.member == member);
                    if (loadedMember.member == null || loadedMember.value != value)
                    {
                        Logger.Instance.Error($"  - {enumType}.{member} 값 불일치: 원본={value}, 로드={loadedMember.value}");
                        allMatch = false;
                    }
                }
                
                Logger.Instance.Log($"  - {enumType}: {loadedMembers.Count}개 멤버 검증 완료");
            }
            
            if (!allMatch)
            {
                result = "[TestEnumDBSharedMemory] 데이터 검증 실패";
                Logger.Instance.Error(result);
                return;
            }
            
            // 7. LoadWithFallback 테스트
            Logger.Instance.Log("[Step 7] LoadWithFallback 테스트");
            EnumDB.Instance.Clear();
            
            // 공유 메모리에서 로드 (이미 존재하므로 성공해야 함)
            if (!EnumDB.Instance.LoadWithFallback(enumDBPath))
            {
                result = "[TestEnumDBSharedMemory] LoadWithFallback 실패";
                Logger.Instance.Error(result);
                return;
            }
            Logger.Instance.Log($"  - LoadWithFallback 성공: {EnumDB.Instance.EnumCount}개 Enum 로드됨");
            
            Logger.Instance.Log("========================================");
            Logger.Instance.Log("[TestEnumDBSharedMemory] 모든 테스트 통과!");
            Logger.Instance.Log("========================================");
            
            result = "[TestEnumDBSharedMemory] 테스트 성공";
        }
        catch (Exception e)
        {
            result = $"[TestEnumDBSharedMemory] 테스트 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
    
    /// <summary>
    /// 현재 EnumDB 데이터를 캡처합니다.
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

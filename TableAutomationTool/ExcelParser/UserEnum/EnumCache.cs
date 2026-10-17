using TableAutomationTool.Common.Utils;

namespace TableAutomationTool.UserEnum;

/// <summary>
/// enum 하나의 정의. C# 소스에서 읽은 결과를 EnumCache 에 넣을 때 쓴다.
/// </summary>
/// <param name="Name">enum 타입 이름. 중첩 enum 은 "Outer.Inner" 형식.</param>
/// <param name="Namespace">enum 이 선언된 네임스페이스. 전역이면 빈 문자열.</param>
/// <param name="Members">선언 순서대로의 (멤버명, 값)</param>
public sealed record EnumDefinition(string Name, string Namespace, List<(string member, int value)> Members);

/// <summary>
/// Enum 데이터를 메모리에 저장하고 관리하는 캐시
///
/// 저장 구조:
/// - _cache: {Enum 타입명: [(멤버명, 값)]} - Enum 데이터
/// - _sheetEnumMap: {시트명: [Enum 타입명]} - 시트(= enum 소스 파일)별 Enum 타입 매핑
/// - _namespaceMap: {Enum 타입명: 네임스페이스} - 코드 생성 시 using 을 붙이는 데 쓴다
/// </summary>
public class EnumCache
{
    private const string ColumnSheetName = "sheetName";
    private const string ColumnNamespace = "namespace";
    private const string ColumnEnumType = "type";
    private const string ColumnMember = "member";
    private const string ColumnValue = "value";

    // Enum 데이터 저장소: {Enum타입명: [(멤버명, 값)]}
    private Dictionary<string, List<(string member, int value)>> _cache = new();

    // 시트-Enum 타입 매핑: {시트명: [Enum타입명]}
    private Dictionary<string, List<string>> _sheetEnumMap = new();

    // Enum 네임스페이스: {Enum타입명: 네임스페이스}
    private Dictionary<string, string> _namespaceMap = new();

    // ============================================
    // 외부 데이터 로드 메서드
    // ============================================

    /// <summary>
    /// 한 시트(= enum 소스 파일 하나)의 enum 정의들을 캐시에 추가합니다.
    /// 이미 등록된 enum 타입과 이름이 겹치면 아무것도 추가하지 않고 실패합니다.
    /// </summary>
    /// <param name="sheetName">시트 이름 (소스 파일 이름, 확장자 제외)</param>
    /// <param name="enums">추가할 enum 정의 목록</param>
    /// <returns>추가 성공 여부</returns>
    public bool LoadFromDefinitions(string sheetName, IReadOnlyList<EnumDefinition> enums)
    {
        if (string.IsNullOrEmpty(sheetName))
        {
            Logger.Instance.Error("[EnumCache] 시트 이름이 비어있습니다");
            return false;
        }

        if (_sheetEnumMap.ContainsKey(sheetName))
        {
            Logger.Instance.Error($"[EnumCache] 시트 '{sheetName}' 가 이미 로드되어 있습니다. enum 소스 파일 이름이 겹치는지 확인하세요.");
            return false;
        }

        var duplicatedInSheet = enums
            .GroupBy(e => e.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicatedInSheet.Count > 0)
        {
            Logger.Instance.Error($"[EnumCache] 시트 '{sheetName}' 안에서 같은 Enum 타입이 여러 번 정의되어 있습니다 - {string.Join(", ", duplicatedInSheet)}");
            return false;
        }

        if (!ValidateDuplicateEnumTypes(sheetName, enums.Select(e => e.Name)))
        {
            return false;
        }

        foreach (var definition in enums)
        {
            _cache[definition.Name] = [.. definition.Members.OrderBy(x => x.value)];
            _namespaceMap[definition.Name] = definition.Namespace ?? string.Empty;
        }
        _sheetEnumMap[sheetName] = enums.Select(e => e.Name).ToList();

        Logger.Instance.Log($"[EnumCache] 시트 로드 완료: {sheetName} ({enums.Count}개의 Enum)");
        return true;
    }

    // ============================================
    // 직렬화 관련 메서드 (EnumDB에서 사용)
    // ============================================

    /// <summary>
    /// 캐시 데이터를 BinaryHelper에서 사용하는 형식으로 변환합니다.
    /// </summary>
    public List<Dictionary<string, object>> ConvertToSerializableData()
    {
        var data = new List<Dictionary<string, object>>();

        foreach (var (sheetName, enumTypes) in _sheetEnumMap)
        {
            foreach (var enumType in enumTypes)
            {
                if (!_cache.TryGetValue(enumType, out var members))
                    continue;

                var enumNamespace = GetEnumNamespace(enumType);
                foreach (var (member, value) in members)
                {
                    data.Add(new Dictionary<string, object>
                    {
                        [ColumnSheetName] = sheetName,
                        [ColumnNamespace] = enumNamespace,
                        [ColumnEnumType] = enumType,
                        [ColumnMember] = member,
                        [ColumnValue] = value
                    });
                }
            }
        }

        return data;
    }

    /// <summary>
    /// 바이너리 직렬화 시 사용할 칼럼 타입 정의를 반환합니다.
    /// </summary>
    public static Dictionary<string, string> GetSerializableColumnTypes()
    {
        return new Dictionary<string, string>
        {
            [ColumnSheetName] = "String",
            [ColumnNamespace] = "String",
            [ColumnEnumType] = "String",
            [ColumnMember] = "String",
            [ColumnValue] = "Int32"
        };
    }

    /// <summary>
    /// 바이너리 데이터를 캐시로 복원합니다.
    /// namespace 칼럼이 없는 이전 형식의 EnumDB 는 네임스페이스를 빈 문자열로 복원합니다.
    /// </summary>
    public void RestoreFromBinaryData(List<Dictionary<string, object>> data)
    {
        foreach (var row in data)
        {
            var sheetName = row.GetValueOrDefault(ColumnSheetName)?.ToString() ?? "Default";
            var enumNamespace = row.GetValueOrDefault(ColumnNamespace)?.ToString() ?? string.Empty;
            var enumType = row.GetValueOrDefault(ColumnEnumType)?.ToString() ?? string.Empty;
            var member = row.GetValueOrDefault(ColumnMember)?.ToString() ?? string.Empty;

            if (!int.TryParse(row.GetValueOrDefault(ColumnValue)?.ToString(), out int value))
            {
                value = 0;
            }

            if (string.IsNullOrEmpty(enumType) || string.IsNullOrEmpty(member))
                continue;

            // _cache에 추가
            if (!_cache.ContainsKey(enumType))
            {
                _cache[enumType] = [];
            }
            _cache[enumType].Add((member, value));

            _namespaceMap[enumType] = enumNamespace;

            // _sheetEnumMap에 추가
            if (!_sheetEnumMap.ContainsKey(sheetName))
            {
                _sheetEnumMap[sheetName] = [];
            }
            if (!_sheetEnumMap[sheetName].Contains(enumType))
            {
                _sheetEnumMap[sheetName].Add(enumType);
            }
        }

        // 각 Enum의 멤버를 값으로 정렬
        foreach (var enumType in _cache.Keys.ToList())
        {
            _cache[enumType] = [.. _cache[enumType].OrderBy(x => x.value)];
        }
    }

    // ============================================
    // 데이터 조회 메서드
    // ============================================

    /// <summary>
    /// 특정 Enum 타입의 모든 멤버를 반환합니다.
    /// </summary>
    public List<(string member, int value)> GetEnumMembers(string enumType)
    {
        if (string.IsNullOrEmpty(enumType))
        {
            Logger.Instance.Warning("[EnumCache] Enum 타입 이름이 비어있습니다");
            return [];
        }

        if (_cache.TryGetValue(enumType, out var members))
        {
            return members;
        }

        Logger.Instance.Warning($"[EnumCache] Enum 타입을 찾을 수 없습니다: {enumType}");
        return [];
    }

    /// <summary>
    /// 특정 Enum 타입이 선언된 네임스페이스를 반환합니다. 전역이거나 모르는 타입이면 빈 문자열입니다.
    /// </summary>
    public string GetEnumNamespace(string enumType)
    {
        if (string.IsNullOrEmpty(enumType))
            return string.Empty;

        return _namespaceMap.TryGetValue(enumType, out var enumNamespace) ? enumNamespace : string.Empty;
    }

    /// <summary>
    /// 특정 시트에서 정의된 Enum 타입 목록을 반환합니다.
    /// </summary>
    public List<string> GetEnumTypesBySheet(string sheetName)
    {
        if (string.IsNullOrEmpty(sheetName))
        {
            Logger.Instance.Warning("[EnumCache] 시트 이름이 비어있습니다");
            return [];
        }

        if (_sheetEnumMap.TryGetValue(sheetName, out var enumTypes))
        {
            return enumTypes;
        }

        Logger.Instance.Warning($"[EnumCache] 시트를 찾을 수 없습니다: {sheetName}");
        return [];
    }

    /// <summary>
    /// 모든 Enum 데이터를 반환합니다.
    /// </summary>
    public IReadOnlyDictionary<string, List<(string member, int value)>> GetAllEnums()
    {
        return _cache.AsReadOnly();
    }

    /// <summary>
    /// 시트-Enum 타입 매핑 정보를 반환합니다.
    /// </summary>
    public IReadOnlyDictionary<string, List<string>> GetSheetEnumMap()
    {
        return _sheetEnumMap.AsReadOnly();
    }

    // ============================================
    // 메모리 관리 메서드
    // ============================================

    /// <summary>
    /// 전체 캐시를 초기화합니다.
    /// </summary>
    public void Clear()
    {
        Logger.Instance.Log($"[EnumCache] 캐시 초기화 ({_cache.Count}개 Enum 타입, {_sheetEnumMap.Count}개 시트 삭제)");
        _cache.Clear();
        _sheetEnumMap.Clear();
        _namespaceMap.Clear();
    }

    /// <summary>
    /// 특정 시트의 Enum 데이터를 삭제합니다.
    /// </summary>
    public void ClearSheet(string sheetName)
    {
        if (_sheetEnumMap.TryGetValue(sheetName, out var enumTypes))
        {
            foreach (var enumType in enumTypes)
            {
                _cache.Remove(enumType);
                _namespaceMap.Remove(enumType);
            }
            _sheetEnumMap.Remove(sheetName);

            Logger.Instance.Log($"[EnumCache] 시트 삭제: {sheetName} ({enumTypes.Count}개 Enum 타입)");
        }
        else
        {
            Logger.Instance.Warning($"[EnumCache] 삭제할 시트를 찾을 수 없습니다: {sheetName}");
        }
    }

    /// <summary>
    /// 특정 Enum 타입만 삭제합니다.
    /// </summary>
    public void RemoveEnum(string enumType)
    {
        if (_cache.Remove(enumType))
        {
            _namespaceMap.Remove(enumType);
            foreach (var sheetName in _sheetEnumMap.Keys.ToList())
            {
                _sheetEnumMap[sheetName].Remove(enumType);
            }

            Logger.Instance.Log($"[EnumCache] Enum 타입 삭제: {enumType}");
        }
        else
        {
            Logger.Instance.Warning($"[EnumCache] 삭제할 Enum 타입을 찾을 수 없습니다: {enumType}");
        }
    }

    // ============================================
    // 상태 조회 메서드
    // ============================================

    public bool HasEnum(string enumType) => _cache.ContainsKey(enumType);
    public bool HasSheet(string sheetName) => _sheetEnumMap.ContainsKey(sheetName);
    public IEnumerable<string> EnumTypes => _cache.Keys;
    public IEnumerable<string> SheetNames => _sheetEnumMap.Keys;
    public int EnumCount => _cache.Count;
    public int SheetCount => _sheetEnumMap.Count;
    public bool IsEmpty => _cache.Count == 0;

    // ============================================
    // 검증 메서드
    // ============================================

    /// <summary>
    /// 추가할 Enum 타입 중 이미 캐시에 존재하는 중복 타입이 있는지 검증합니다.
    /// </summary>
    private bool ValidateDuplicateEnumTypes(string sheetName, IEnumerable<string> enumTypes)
    {
        var duplicateEnumTypes = enumTypes
            .Where(enumType => _cache.ContainsKey(enumType))
            .ToList();

        if (duplicateEnumTypes.Count > 0)
        {
            var owners = duplicateEnumTypes
                .Select(t => $"{t}({_sheetEnumMap.FirstOrDefault(kv => kv.Value.Contains(t)).Key})");
            Logger.Instance.Error($"[EnumCache] 시트 '{sheetName}' 로드 실패: 같은 이름의 Enum 타입이 이미 정의되어 있습니다 - {string.Join(", ", owners)}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 디버그용: 현재 캐시 상태를 출력합니다.
    /// </summary>
    public void PrintCacheInfo()
    {
        Logger.Instance.Log($"[EnumCache] === 캐시 정보 ===");
        Logger.Instance.Log($"[EnumCache] 총 시트: {SheetCount}, 총 Enum 타입: {EnumCount}");

        foreach (var (sheetName, enumTypes) in _sheetEnumMap)
        {
            Logger.Instance.Log($"[EnumCache] 시트: {sheetName}");
            foreach (var enumType in enumTypes)
            {
                var members = _cache[enumType];
                Logger.Instance.Log($"[EnumCache]   - {enumType}: {members.Count}개 멤버");
                foreach (var (member, value) in members)
                {
                    Logger.Instance.Log($"[EnumCache]     * {member} = {value}");
                }
            }
        }
        Logger.Instance.Log($"[EnumCache] ==================");
    }

    // ============================================
    // IPC를 통한 로드 메서드
    // ============================================

    /// <summary>
    /// IPC 공유 메모리에서 받은 Enum 데이터를 로드합니다.
    /// SharedEnumMemoryHandler에서 내부적으로 사용됩니다.
    /// </summary>
    /// <param name="enumType">Enum 타입명</param>
    /// <param name="members">멤버 리스트</param>
    /// <param name="enumNamespace">Enum 네임스페이스</param>
    internal void LoadEnumDirectly(string enumType, List<(string member, int value)> members, string enumNamespace)
    {
        if (string.IsNullOrEmpty(enumType) || members == null || members.Count == 0)
        {
            Logger.Instance.Warning($"[EnumCache] LoadEnumDirectly: 유효하지 않은 데이터 - {enumType}");
            return;
        }

        _cache[enumType] = [.. members.OrderBy(x => x.value)];
        _namespaceMap[enumType] = enumNamespace ?? string.Empty;

        // IPC 로드 시에는 시트 정보가 없으므로 "_IPC_" 시트로 등록
        const string ipcSheetName = "_IPC_";
        if (!_sheetEnumMap.ContainsKey(ipcSheetName))
        {
            _sheetEnumMap[ipcSheetName] = [];
        }
        if (!_sheetEnumMap[ipcSheetName].Contains(enumType))
        {
            _sheetEnumMap[ipcSheetName].Add(enumType);
        }

        Logger.Instance.Log($"[EnumCache] IPC 직접 로드: {enumType} ({members.Count}개 멤버)");
    }
}

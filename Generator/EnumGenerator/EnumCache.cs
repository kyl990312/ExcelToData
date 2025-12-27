using Common.Utils;
using ExcelParser;

namespace EnumGenerator;

/// <summary>
/// Enum 데이터를 메모리에 저장하고 관리하는 캐시
/// 
/// 저장 구조:
/// - _cache: {Enum 타입명: [(멤버명, 값)]} - Enum 데이터
/// - _sheetEnumMap: {시트명: [Enum 타입명]} - 시트별 Enum 타입 매핑
/// </summary>
public class EnumCache
{
    private const string ColumnSheetName = "sheetName";
    private const string ColumnEnumType = "type";
    private const string ColumnMember = "member";
    private const string ColumnValue = "value";

    // Enum 데이터 저장소: {Enum타입명: [(멤버명, 값)]}
    private Dictionary<string, List<(string member, int value)>> _cache = new();
    
    // 시트-Enum 타입 매핑: {시트명: [Enum타입명]}
    private Dictionary<string, List<string>> _sheetEnumMap = new();

    // ============================================
    // 외부 데이터 로드 메서드
    // ============================================

    /// <summary>
    /// 외부에서 로드한 CustomDataTable로부터 Enum 데이터를 저장합니다.
    /// 테이블의 Name 속성을 시트 이름으로 사용합니다.
    /// </summary>
    /// <param name="table">Enum 데이터를 포함하는 CustomDataTable</param>
    /// <returns>로드 성공 여부</returns>
    public bool LoadFromTable(CustomDataTable table)
    {
        // 1. 테이블 유효성 검사
        if (!ValidateTable(table))
        {
            return false;
        }

        try
        {
            Logger.Instance.Log($"[EnumCache] 테이블에서 Enum 로드 시작: {table.Name}");

            // 2. 테이블에서 Enum 데이터 추출
            var extractedEnums = ExtractEnumsFromTable(table);
            if (extractedEnums.Count == 0)
            {
                Logger.Instance.Warning($"[EnumCache] Enum 데이터가 없습니다: {table.Name}");
                return false;
            }

            // 3. 중복 Enum 타입 검사
            if (!ValidateDuplicateEnumTypes(table.Name, extractedEnums.Keys))
            {
                return false;
            }

            // 4. 검증 통과 시 캐시에 추가
            foreach (var (enumType, members) in extractedEnums)
            {
                _cache[enumType] = members;
            }
            _sheetEnumMap[table.Name] = extractedEnums.Keys.ToList();

            Logger.Instance.Log($"[EnumCache] 테이블 로드 완료: {table.Name} ({extractedEnums.Count}개의 Enum)");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[EnumCache] 테이블 로드 중 오류: {table.Name} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 여러 CustomDataTable을 한 번에 로드합니다.
    /// 각 테이블의 Name 속성을 시트 이름으로 사용합니다.
    /// </summary>
    /// <param name="tables">로드할 CustomDataTable 컬렉션</param>
    /// <returns>모든 테이블 로드 성공 여부</returns>
    public bool LoadFromTables(IEnumerable<CustomDataTable> tables)
    {
        if (tables == null)
        {
            Logger.Instance.Error("[EnumCache] 테이블 컬렉션이 null입니다");
            return false;
        }

        var tableList = tables.ToList();
        Logger.Instance.Log($"[EnumCache] 다중 테이블 로드 시작: {tableList.Count}개");

        bool allSuccess = true;
        foreach (var table in tableList)
        {
            if (!LoadFromTable(table))
            {
                allSuccess = false;
            }
        }

        Logger.Instance.Log($"[EnumCache] 다중 테이블 로드 완료: {_cache.Count}개 Enum 타입 처리됨");
        return allSuccess;
    }

    // ============================================
    // 직렬화 지원 메서드 (EnumDB에서 사용)
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

                foreach (var (member, value) in members)
                {
                    data.Add(new Dictionary<string, object>
                    {
                        [ColumnSheetName] = sheetName,
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
    /// 바이너리 데이터를 캐시로 복원합니다.
    /// </summary>
    public void RestoreFromBinaryData(List<Dictionary<string, object>> data)
    {
        foreach (var row in data)
        {
            var sheetName = row.GetValueOrDefault(ColumnSheetName)?.ToString() ?? "Default";
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
    /// 특정 Enum 타입의 멤버 데이터를 반환합니다.
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
        Logger.Instance.Log($"[EnumCache] 캐시 초기화 ({_cache.Count}개 Enum 타입, {_sheetEnumMap.Count}개 시트 제거)");
        _cache.Clear();
        _sheetEnumMap.Clear();
    }

    /// <summary>
    /// 특정 시트의 Enum 데이터를 제거합니다.
    /// </summary>
    public void ClearSheet(string sheetName)
    {
        if (_sheetEnumMap.TryGetValue(sheetName, out var enumTypes))
        {
            foreach (var enumType in enumTypes)
            {
                _cache.Remove(enumType);
            }
            _sheetEnumMap.Remove(sheetName);
            
            Logger.Instance.Log($"[EnumCache] 시트 제거: {sheetName} ({enumTypes.Count}개 Enum 타입)");
        }
        else
        {
            Logger.Instance.Warning($"[EnumCache] 제거할 시트를 찾을 수 없습니다: {sheetName}");
        }
    }

    /// <summary>
    /// 특정 Enum 타입만 제거합니다.
    /// </summary>
    public void RemoveEnum(string enumType)
    {
        if (_cache.Remove(enumType))
        {
            foreach (var sheetName in _sheetEnumMap.Keys.ToList())
            {
                _sheetEnumMap[sheetName].Remove(enumType);
            }
            
            Logger.Instance.Log($"[EnumCache] Enum 타입 제거: {enumType}");
        }
        else
        {
            Logger.Instance.Warning($"[EnumCache] 제거할 Enum 타입을 찾을 수 없습니다: {enumType}");
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
    /// 테이블의 유효성을 검증합니다.
    /// </summary>
    private static bool ValidateTable(CustomDataTable table)
    {
        if (table == null)
        {
            Logger.Instance.Error("[EnumCache] 테이블이 null입니다");
            return false;
        }

        if (string.IsNullOrEmpty(table.Name))
        {
            Logger.Instance.Error("[EnumCache] 테이블 이름이 비어있습니다");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 추출된 Enum 타입 중 이미 캐시에 존재하는 중복 타입이 있는지 검증합니다.
    /// </summary>
    private bool ValidateDuplicateEnumTypes(string tableName, IEnumerable<string> enumTypes)
    {
        var duplicateEnumTypes = enumTypes
            .Where(enumType => _cache.ContainsKey(enumType))
            .ToList();

        if (duplicateEnumTypes.Count > 0)
        {
            Logger.Instance.Error($"[EnumCache] 시트 '{tableName}' 로드 실패: 다음 Enum 타입이 이미 정의되어 있습니다 - {string.Join(", ", duplicateEnumTypes)}");
            return false;
        }

        return true;
    }

    // ============================================
    // 내부 메서드
    // ============================================

    /// <summary>
    /// CustomDataTable에서 Enum 데이터를 추출합니다.
    /// </summary>
    private Dictionary<string, List<(string member, int value)>> ExtractEnumsFromTable(CustomDataTable table)
    {
        var extractedEnums = new Dictionary<string, List<(string member, int value)>>();

        if (!table.HasColumn(ColumnEnumType) ||
            !table.HasColumn(ColumnMember) ||
            !table.HasColumn(ColumnValue))
        {
            Logger.Instance.Error($"[EnumCache] 필수 컬럼이 없습니다. 필요: {ColumnEnumType}, {ColumnMember}, {ColumnValue}");
            return extractedEnums;
        }

        try
        {
            for (int i = 0; i < table.RowCount; i++)
            {
                try
                {
                    var enumType = table.GetValue<string>(ColumnEnumType, i);
                    var member = table.GetValue<string>(ColumnMember, i);
                    var valueObj = table.GetValue(ColumnValue, i);

                    if (string.IsNullOrEmpty(enumType) || string.IsNullOrEmpty(member))
                    {
                        Logger.Instance.Warning($"[EnumCache] 행 {i}: enumType 또는 member가 비어있습니다");
                        continue;
                    }

                    if (!int.TryParse(valueObj?.ToString(), out int value))
                    {
                        Logger.Instance.Warning($"[EnumCache] 행 {i}: value를 정수로 변환할 수 없습니다 ('{valueObj}')");
                        value = 0;
                    }

                    if (!extractedEnums.ContainsKey(enumType))
                    {
                        extractedEnums[enumType] = [];
                    }

                    extractedEnums[enumType].Add((member, value));
                    Logger.Instance.Log($"[EnumCache] Enum 추출: {enumType}.{member} = {value}");
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"[EnumCache] 행 {i} 처리 중 오류: {ex.Message}");
                    continue;
                }
            }

            // 각 Enum의 멤버를 값으로 정렬
            foreach (var enumType in extractedEnums.Keys.ToList())
            {
                extractedEnums[enumType] = [.. extractedEnums[enumType].OrderBy(x => x.value)];
            }

            Logger.Instance.Log($"[EnumCache] Enum 추출 완료: {extractedEnums.Count}개 타입");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[EnumCache] Enum 데이터 추출 중 오류: {ex.Message}");
        }

        return extractedEnums;
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
    // IPC용 직접 로드 메서드
    // ============================================
    
    /// <summary>
    /// IPC 공유 메모리에서 직접 Enum 데이터를 로드합니다.
    /// SharedEnumMemoryHandler에서 내부적으로 사용됩니다.
    /// </summary>
    /// <param name="enumType">Enum 타입명</param>
    /// <param name="members">멤버 리스트</param>
    internal void LoadEnumDirectly(string enumType, List<(string member, int value)> members)
    {
        if (string.IsNullOrEmpty(enumType) || members == null || members.Count == 0)
        {
            Logger.Instance.Warning($"[EnumCache] LoadEnumDirectly: 유효하지 않은 데이터 - {enumType}");
            return;
        }
        
        _cache[enumType] = [.. members.OrderBy(x => x.value)];
        
        // IPC 로드 시에는 시트 매핑이 없으므로 "_IPC_" 시트로 등록
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
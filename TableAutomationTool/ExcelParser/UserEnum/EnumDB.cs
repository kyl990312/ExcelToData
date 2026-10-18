using BinaryParser;
using TableAutomationTool.Common.Utils;

namespace TableAutomationTool.UserEnum;

/// <summary>
/// EnumCache를 싱글톤으로 관리하는 전역 접근 클래스
/// - 프로세스 내 Enum 데이터 단일 접근점
/// - 바이너리 파일 및 IPC를 통한 데이터 로드/저장
/// </summary>
public sealed class EnumDB : IDisposable
{
    private static readonly Lazy<EnumDB> _instance = new(() => new EnumDB());
    
    /// <summary>
    /// EnumDB 싱글톤 인스턴스
    /// </summary>
    public static EnumDB Instance => _instance.Value;
    
    /// <summary>
    /// 내부 EnumCache 인스턴스
    /// </summary>
    public EnumCache Cache { get; }
    
    /// <summary>
    /// IPC 공유 메모리 핸들러
    /// </summary>
    private SharedEnumMemoryHandler? _sharedMemoryHandler;
    
    private readonly BinaryParser.BinaryReader _binaryReader = new();
    private readonly BinaryParser.BinaryWriter _binaryWriter = new();
    
    private bool _disposed;
    
    private EnumDB()
    {
        Cache = new EnumCache();
    }
    
    // ============================================
    // 바이너리 파일 로드/저장
    // ============================================
    
    /// <summary>
    /// 바이너리 파일에서 EnumCache 데이터를 로드합니다.
    /// </summary>
    /// <param name="filePath">로드할 바이너리 파일 경로</param>
    /// <returns>로드 성공 여부</returns>
    public bool LoadFromBinary(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Logger.Instance.Error("[EnumDB] 파일 경로가 비어있습니다");
            return false;
        }

        if (!File.Exists(filePath))
        {
            Logger.Instance.Error($"[EnumDB] 파일을 찾을 수 없습니다: {filePath}");
            return false;
        }

        try
        {
            var data = _binaryReader.ReadFromFile(filePath);
            if (data == null || data.Count == 0)
            {
                Logger.Instance.Warning($"[EnumDB] 바이너리 파일이 비어있습니다: {filePath}");
                return false;
            }

            Cache.Clear();
            Cache.RestoreFromBinaryData(data);

            Logger.Instance.Log($"[EnumDB] 바이너리 로드 완료: {filePath} ({Cache.EnumCount}개 Enum 타입, {Cache.SheetCount}개 시트)");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[EnumDB] 바이너리 로드 중 오류: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 현재 캐시 데이터를 바이너리 파일로 저장합니다.
    /// </summary>
    /// <param name="filePath">저장할 파일 경로</param>
    /// <returns>저장 성공 여부</returns>
    public bool SaveToBinary(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Logger.Instance.Error("[EnumDB] 파일 경로가 비어있습니다");
            return false;
        }

        try
        {
            var data = Cache.ConvertToSerializableData();
            var columnTypes = EnumCache.GetSerializableColumnTypes();
            _binaryWriter.WriteToFile(columnTypes, data, filePath);

            Logger.Instance.Log($"[EnumDB] 바이너리 저장 완료: {filePath} ({Cache.EnumCount}개 Enum 타입, {Cache.SheetCount}개 시트)");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[EnumDB] 바이너리 저장 중 오류: {ex.Message}");
            return false;
        }
    }
    
    // ============================================
    // IPC 공유 메모리
    // ============================================
    
    /// <summary>
    /// 현재 캐시 데이터를 공유 메모리에 저장합니다.
    /// </summary>
    /// <returns>저장 성공 여부</returns>
    public bool WriteToSharedMemory()
    {
        _sharedMemoryHandler ??= new SharedEnumMemoryHandler();
        return _sharedMemoryHandler.Write(Cache);
    }
    
    /// <summary>
    /// 공유 메모리에서 데이터를 로드합니다.
    /// </summary>
    /// <returns>로드 성공 여부</returns>
    public bool ReadFromSharedMemory()
    {
        _sharedMemoryHandler ??= new SharedEnumMemoryHandler();
        return _sharedMemoryHandler.Read(Cache);
    }
    
    /// <summary>
    /// 공유 메모리에서 로드 시도, 실패 시 바이너리 파일에서 로드
    /// </summary>
    /// <param name="fallbackBinaryPath">폴백용 바이너리 파일 경로</param>
    /// <returns>로드 성공 여부</returns>
    public bool LoadWithFallback(string fallbackBinaryPath)
    {
        // 1차: 공유 메모리 시도
        if (ReadFromSharedMemory())
        {
            Logger.Instance.Log("[EnumDB] IPC에서 로드 성공");
            return true;
        }
        
        // 2차: 바이너리 파일 폴백
        if (File.Exists(fallbackBinaryPath))
        {
            Logger.Instance.Log("[EnumDB] IPC 실패, 바이너리 파일에서 로드");
            return LoadFromBinary(fallbackBinaryPath);
        }
        
        Logger.Instance.Warning("[EnumDB] 로드 실패: IPC 없음, 바이너리 파일 없음");
        return false;
    }
    
    /// <summary>
    /// 공유 메모리에서 로드 시도, 실패 시 바이너리 파일에서 로드 후 공유 메모리에 캐싱
    /// </summary>
    /// <param name="fallbackBinaryPath">폴백용 바이너리 파일 경로</param>
    /// <param name="cacheToSharedMemory">폴백 로드 후 공유 메모리에 캐싱 여부</param>
    /// <returns>로드 성공 여부</returns>
    public bool LoadWithFallback(string fallbackBinaryPath, bool cacheToSharedMemory)
    {
        // 1차: 공유 메모리 시도
        if (ReadFromSharedMemory())
        {
            Logger.Instance.Log("[EnumDB] IPC에서 로드 성공");
            return true;
        }
        
        // 2차: 바이너리 파일 폴백
        if (!File.Exists(fallbackBinaryPath))
        {
            Logger.Instance.Warning("[EnumDB] 로드 실패: IPC 없음, 바이너리 파일 없음");
            return false;
        }
        
        Logger.Instance.Log("[EnumDB] IPC 실패, 바이너리 파일에서 로드");
        var result = LoadFromBinary(fallbackBinaryPath);
        
        // 3차: 폴백 성공 시 공유 메모리에 캐싱
        if (result && cacheToSharedMemory)
        {
            WriteToSharedMemory();
            Logger.Instance.Log("[EnumDB] 바이너리 데이터를 공유 메모리에 캐싱 완료");
        }
        
        return result;
    }
    
    /// <summary>
    /// 공유 메모리가 존재하는지 확인합니다.
    /// </summary>
    public bool SharedMemoryExists()
    {
        _sharedMemoryHandler ??= new SharedEnumMemoryHandler();
        return _sharedMemoryHandler.Exists();
    }
    
    // ============================================
    // 편의 메서드 (EnumCache 위임)
    // ============================================
    
    /// <summary>
    /// 특정 Enum 타입의 멤버 데이터를 반환합니다.
    /// </summary>
    public List<(string member, int value)> GetEnumMembers(string enumType) 
        => Cache.GetEnumMembers(enumType);
    
    /// <summary>
    /// 특정 시트에서 정의된 Enum 타입 목록을 반환합니다.
    /// </summary>
    public List<string> GetEnumTypesBySheet(string sheetName) 
        => Cache.GetEnumTypesBySheet(sheetName);
    
    /// <summary>
    /// 모든 Enum 데이터를 반환합니다.
    /// </summary>
    public IReadOnlyDictionary<string, List<(string member, int value)>> GetAllEnums() 
        => Cache.GetAllEnums();
    
    /// <summary>
    /// Enum 타입 존재 여부를 확인합니다.
    /// </summary>
    public bool HasEnum(string enumType) => Cache.HasEnum(enumType);

    /// <summary>
    /// Enum 타입이 선언된 네임스페이스를 조회합니다. 전역이거나 모르는 타입이면 빈 문자열입니다.
    /// </summary>
    public string GetEnumNamespace(string enumType) => Cache.GetEnumNamespace(enumType);
    
    /// <summary>
    /// Enum 멤버 이름으로 값을 조회합니다.
    /// </summary>
    public int? GetEnumValue(string enumType, string memberName)
    {
        var members = Cache.GetEnumMembers(enumType);
        var member = members.Find(m => m.member == memberName);
        return member.member != null ? member.value : null;
    }
    
    /// <summary>
    /// Enum 값으로 멤버 이름을 조회합니다.
    /// </summary>
    public string? GetEnumMemberName(string enumType, int value)
    {
        var members = Cache.GetEnumMembers(enumType);
        var member = members.Find(m => m.value == value);
        return member.member;
    }
    
    // ============================================
    // 상태 조회 (EnumCache 위임)
    // ============================================
    
    public bool IsEmpty => Cache.IsEmpty;
    public int EnumCount => Cache.EnumCount;
    public IEnumerable<string> EnumTypes => Cache.EnumTypes;
    
    // ============================================
    // 메모리 관리
    // ============================================
    
    /// <summary>
    /// 전체 캐시를 초기화합니다.
    /// </summary>
    public void Clear() => Cache.Clear();
    
    /// <summary>
    /// 디버그용: 현재 캐시 상태를 출력합니다.
    /// </summary>
    public void PrintInfo() => Cache.PrintCacheInfo();
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _sharedMemoryHandler?.Dispose();
        _disposed = true;
    }
}
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using TableAutomationTool.Common.Utils;

namespace TableAutomationTool.UserEnum;

/// <summary>
/// 프로세스 간 Enum 데이터 공유를 위한 Memory-Mapped File 기반 IPC 핸들러
/// EnumDB에서 인스턴스를 생성하여 사용합니다.
/// </summary>
public sealed class SharedEnumMemoryHandler : IDisposable
{
    private const string DefaultMapName = "TableAutomationTool_EnumDB";
    private const string DefaultMutexName = "TableAutomationTool_EnumDB_Mutex";
    private const int DefaultCapacity = 10 * 1024 * 1024; // 10MB 기본 용량
    
    private MemoryMappedFile? _mmf;
    private Mutex? _mutex;
    private readonly string _mapName;
    private readonly int _capacity;
    private bool _disposed;
    
    public SharedEnumMemoryHandler() : this(DefaultMapName, DefaultCapacity) { }
    
    /// <summary>
    /// 커스텀 설정으로 핸들러 생성
    /// </summary>
    /// <param name="mapName">Memory-Mapped File 이름</param>
    /// <param name="capacity">최대 용량 (바이트)</param>
    public SharedEnumMemoryHandler(string mapName, int capacity)
    {
        _mapName = mapName;
        _capacity = capacity;
    }
    
    // ============================================
    // 공유 메모리 쓰기 (Producer)
    // ============================================
    
    /// <summary>
    /// EnumCache 데이터를 공유 메모리에 저장합니다.
    /// </summary>
    /// <param name="cache">저장할 EnumCache</param>
    /// <returns>저장 성공 여부</returns>
    public bool Write(EnumCache cache)
    {
        if (cache.IsEmpty)
        {
            Logger.Instance.Warning("[SharedEnumMemoryHandler] EnumCache가 비어있습니다");
            return false;
        }
        
        try
        {
            EnsureMutex();
            
            if (!_mutex!.WaitOne(TimeSpan.FromSeconds(5)))
            {
                Logger.Instance.Error("[SharedEnumMemoryHandler] Mutex 획득 실패 (타임아웃)");
                return false;
            }
            
            try
            {
                var data = SerializeEnumCache(cache);
                var bytes = Encoding.UTF8.GetBytes(data);
                
                if (bytes.Length > _capacity - sizeof(int))
                {
                    Logger.Instance.Error($"[SharedEnumMemoryHandler] 데이터 크기({bytes.Length})가 용량({_capacity})을 초과합니다");
                    return false;
                }
                
                _mmf?.Dispose();
                _mmf = MemoryMappedFile.CreateOrOpen(_mapName, _capacity);
                
                using var accessor = _mmf.CreateViewAccessor();
                accessor.Write(0, bytes.Length);
                accessor.WriteArray(sizeof(int), bytes, 0, bytes.Length);
                
                Logger.Instance.Log($"[SharedEnumMemoryHandler] 공유 메모리에 저장 완료: {bytes.Length} bytes, {cache.EnumCount} enum types");
                return true;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[SharedEnumMemoryHandler] 쓰기 오류: {ex.Message}");
            return false;
        }
    }
    
    // ============================================
    // 공유 메모리 읽기 (Consumer)
    // ============================================
    
    /// <summary>
    /// 공유 메모리에서 EnumCache로 데이터를 로드합니다.
    /// </summary>
    /// <param name="cache">데이터를 로드할 EnumCache</param>
    /// <returns>로드 성공 여부</returns>
    public bool Read(EnumCache cache)
    {
        try
        {
            EnsureMutex();
            
            if (!_mutex!.WaitOne(TimeSpan.FromSeconds(5)))
            {
                Logger.Instance.Error("[SharedEnumMemoryHandler] Mutex 획득 실패 (타임아웃)");
                return false;
            }
            
            try
            {
                try
                {
                    _mmf = MemoryMappedFile.OpenExisting(_mapName);
                }
                catch (FileNotFoundException)
                {
                    Logger.Instance.Warning("[SharedEnumMemoryHandler] 공유 메모리가 존재하지 않습니다");
                    return false;
                }
                
                using var accessor = _mmf.CreateViewAccessor();
                int dataLength = accessor.ReadInt32(0);
                
                if (dataLength <= 0 || dataLength > _capacity - sizeof(int))
                {
                    Logger.Instance.Warning($"[SharedEnumMemoryHandler] 유효하지 않은 데이터 길이: {dataLength}");
                    return false;
                }
                
                var bytes = new byte[dataLength];
                accessor.ReadArray(sizeof(int), bytes, 0, dataLength);
                
                var data = Encoding.UTF8.GetString(bytes);
                DeserializeToEnumCache(data, cache);
                
                Logger.Instance.Log($"[SharedEnumMemoryHandler] 공유 메모리에서 로드 완료: {dataLength} bytes, {cache.EnumCount} enum types");
                return true;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[SharedEnumMemoryHandler] 읽기 오류: {ex.Message}");
            return false;
        }
    }
    
    // ============================================
    // 상태 확인
    // ============================================
    
    /// <summary>
    /// 공유 메모리가 존재하는지 확인합니다.
    /// </summary>
    public bool Exists()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(_mapName);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
    
    /// <summary>
    /// 공유 메모리의 데이터 크기를 반환합니다.
    /// </summary>
    public int GetDataSize()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(_mapName);
            using var accessor = mmf.CreateViewAccessor();
            return accessor.ReadInt32(0);
        }
        catch
        {
            return 0;
        }
    }
    
    // ============================================
    // 직렬화/역직렬화
    // ============================================
    
    private static string SerializeEnumCache(EnumCache cache)
    {
        var serializableData = new SharedEnumData
        {
            Version = 2,
            Timestamp = DateTime.UtcNow,
            Enums = []
        };

        foreach (var enumType in cache.EnumTypes)
        {
            var members = cache.GetEnumMembers(enumType);
            serializableData.Enums[enumType] = members
                .Select(m => new EnumMemberData { Name = m.member, Value = m.value })
                .ToList();
            serializableData.Namespaces[enumType] = cache.GetEnumNamespace(enumType);
        }
        
        return JsonSerializer.Serialize(serializableData, new JsonSerializerOptions 
        { 
            WriteIndented = false 
        });
    }
    
    private static void DeserializeToEnumCache(string json, EnumCache cache)
    {
        var data = JsonSerializer.Deserialize<SharedEnumData>(json);
        
        if (data?.Enums == null)
        {
            Logger.Instance.Warning("[SharedEnumMemoryHandler] 역직렬화된 데이터가 비어있습니다");
            return;
        }
        
        cache.Clear();
        
        foreach (var (enumType, members) in data.Enums)
        {
            var memberTuples = members.Select(m => (m.Name, m.Value)).ToList();
            var enumNamespace = data.Namespaces?.GetValueOrDefault(enumType) ?? string.Empty;
            cache.LoadEnumDirectly(enumType, memberTuples, enumNamespace);
        }
    }
    
    // ============================================
    // 리소스 관리
    // ============================================
    
    private void EnsureMutex()
    {
        _mutex ??= new Mutex(false, DefaultMutexName);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _mmf?.Dispose();
        _mutex?.Dispose();
        _disposed = true;
        
        Logger.Instance.Log("[SharedEnumMemoryHandler] 리소스 해제 완료");
    }
    
    // ============================================
    // 직렬화용 내부 클래스
    // ============================================
    
    private sealed class SharedEnumData
    {
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, List<EnumMemberData>> Enums { get; set; } = [];
        public Dictionary<string, string> Namespaces { get; set; } = [];
    }
    
    private sealed class EnumMemberData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
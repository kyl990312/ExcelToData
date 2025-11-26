using System.Text;

namespace Common.Utils;

/// <summary>
/// EnumDB에서 로드한 사용자 정의 Enum 정보를 관리하는 클래스
/// </summary>
public class UserEnumInfo : Singleton<UserEnumInfo>
{
    /// <summary>
    /// Enum 데이터 저장소
    /// Key: EnumTypeName (예: "ItemType")
    /// Value: Dictionary(MemberName, MemberValue) (예: {"Weapon": 1, "Armor": 2})
    /// </summary>
    private Dictionary<string, Dictionary<string, int>> _enumMap = new();

    /// <summary>
    /// 역방향 조회용 캐시
    /// Key: EnumTypeName
    /// Value: Dictionary(MemberValue, MemberName)
    /// </summary>
    private Dictionary<string, Dictionary<int, string>> _reverseMap = new();

    /// <summary>
    /// EnumDB 파일에서 데이터를 로드합니다.
    /// </summary>
    /// <param name="enumDBPath">EnumDB 바이너리 파일 경로</param>
    /// <returns>로드 성공 여부</returns>
    public bool LoadFromFile(string enumDBPath)
    {
        try
        {
            if (!File.Exists(enumDBPath))
            {
                Logger.Instance.Warning($"[UserEnumInfo] EnumDB file not found: {enumDBPath}");
                return false;
            }

            Clear();

            using var fileStream = new FileStream(enumDBPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fileStream, Encoding.UTF8);

            // Header 읽기
            byte[] magic = reader.ReadBytes(4);
            string magicStr = Encoding.ASCII.GetString(magic);
            if (magicStr != "ENDB")
            {
                Logger.Instance.Error($"[UserEnumInfo] Invalid EnumDB file format: {enumDBPath}");
                return false;
            }

            int version = reader.ReadInt32();
            if (version != 1)
            {
                Logger.Instance.Warning($"[UserEnumInfo] Unsupported EnumDB version: {version}");
            }

            int enumCount = reader.ReadInt32();

            // Enum Entries 읽기
            for (int i = 0; i < enumCount; i++)
            {
                string enumName = reader.ReadString();
                int memberCount = reader.ReadInt32();

                var members = new Dictionary<string, int>();
                var reverseMembers = new Dictionary<int, string>();

                for (int j = 0; j < memberCount; j++)
                {
                    string memberName = reader.ReadString();
                    int memberValue = reader.ReadInt32();

                    members[memberName] = memberValue;
                    reverseMembers[memberValue] = memberName;
                }

                _enumMap[enumName] = members;
                _reverseMap[enumName] = reverseMembers;
            }

            Logger.Instance.Log($"[UserEnumInfo] Loaded {_enumMap.Count} enums from {enumDBPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[UserEnumInfo] Failed to load EnumDB: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 지정된 타입이 사용자 정의 Enum인지 확인합니다.
    /// </summary>
    /// <param name="typeName">확인할 타입명</param>
    /// <returns>사용자 정의 Enum 여부</returns>
    public bool IsUserDefinedEnum(string typeName)
    {
        return _enumMap.ContainsKey(typeName);
    }

    /// <summary>
    /// Enum 멤버명으로 int 값을 조회합니다.
    /// </summary>
    /// <param name="typeName">Enum 타입명</param>
    /// <param name="memberName">멤버명</param>
    /// <returns>멤버 값 (찾지 못하면 0)</returns>
    public int GetEnumValue(string typeName, string memberName)
    {
        if (_enumMap.TryGetValue(typeName, out var members))
        {
            if (members.TryGetValue(memberName, out int value))
            {
                return value;
            }
            Logger.Instance.Warning($"[UserEnumInfo] Member '{memberName}' not found in enum '{typeName}'");
        }
        else
        {
            Logger.Instance.Warning($"[UserEnumInfo] Enum type '{typeName}' not found");
        }
        return 0;
    }

    /// <summary>
    /// int 값으로 Enum 멤버명을 조회합니다.
    /// </summary>
    /// <param name="typeName">Enum 타입명</param>
    /// <param name="value">멤버 값</param>
    /// <returns>멤버명 (찾지 못하면 빈 문자열)</returns>
    public string GetEnumMember(string typeName, int value)
    {
        if (_reverseMap.TryGetValue(typeName, out var members))
        {
            if (members.TryGetValue(value, out string memberName))
            {
                return memberName;
            }
            Logger.Instance.Warning($"[UserEnumInfo] Value '{value}' not found in enum '{typeName}'");
        }
        else
        {
            Logger.Instance.Warning($"[UserEnumInfo] Enum type '{typeName}' not found");
        }
        return string.Empty;
    }

    /// <summary>
    /// 특정 Enum 타입의 모든 멤버 정보를 반환합니다.
    /// </summary>
    /// <param name="typeName">Enum 타입명</param>
    /// <returns>멤버 Dictionary (없으면 빈 Dictionary)</returns>
    public IReadOnlyDictionary<string, int> GetEnumMembers(string typeName)
    {
        if (_enumMap.TryGetValue(typeName, out var members))
        {
            return members;
        }
        return new Dictionary<string, int>();
    }

    /// <summary>
    /// 등록된 모든 Enum 타입명을 반환합니다.
    /// </summary>
    /// <returns>Enum 타입명 목록</returns>
    public IEnumerable<string> GetAllEnumTypes()
    {
        return _enumMap.Keys;
    }

    /// <summary>
    /// 등록된 Enum 개수를 반환합니다.
    /// </summary>
    public int EnumCount => _enumMap.Count;

    /// <summary>
    /// 모든 Enum 데이터를 초기화합니다.
    /// </summary>
    public void Clear()
    {
        _enumMap.Clear();
        _reverseMap.Clear();
    }

    /// <summary>
    /// 디버그용: 현재 로드된 Enum 정보를 출력합니다.
    /// </summary>
    public void PrintEnumInfo()
    {
        Logger.Instance.Log($"[UserEnumInfo] Total enum types: {_enumMap.Count}");

        foreach (var enumEntry in _enumMap)
        {
            Logger.Instance.Log($"[UserEnumInfo] {enumEntry.Key}: {enumEntry.Value.Count} members");
            foreach (var member in enumEntry.Value)
            {
                Logger.Instance.Log($"[UserEnumInfo]   - {member.Key} = {member.Value}");
            }
        }
    }
}

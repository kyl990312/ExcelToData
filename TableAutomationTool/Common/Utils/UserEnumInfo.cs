using System.Text;

namespace TableAutomationTool.Common.Utils;

/// <summary>
/// EnumDB?ì„œ ë¡œë“œ???¬ìš©???•ì˜ Enum ?•ë³´ë¥?ê´€ë¦¬í•˜???´ë˜??/// </summary>
public class UserEnumInfo : Singleton<UserEnumInfo>
{
    /// <summary>
    /// Enum ?°ì´???€?¥ì†Œ
    /// Key: EnumTypeName (?? "ItemType")
    /// Value: Dictionary(MemberName, MemberValue) (?? {"Weapon": 1, "Armor": 2})
    /// </summary>
    private Dictionary<string, Dictionary<string, int>> _enumMap = new();

    /// <summary>
    /// ??°©??ì¡°íšŒ??ìºì‹œ
    /// Key: EnumTypeName
    /// Value: Dictionary(MemberValue, MemberName)
    /// </summary>
    private Dictionary<string, Dictionary<int, string>> _reverseMap = new();

    /// <summary>
    /// EnumDB ?Œì¼?ì„œ ?°ì´?°ë? ë¡œë“œ?©ë‹ˆ??
    /// </summary>
    /// <param name="enumDBPath">EnumDB ë°”ì´?ˆë¦¬ ?Œì¼ ê²½ë¡œ</param>
    /// <returns>ë¡œë“œ ?±ê³µ ?¬ë?</returns>
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

            // Header ?½ê¸°
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

            // Enum Entries ?½ê¸°
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
    /// ì§€?•ëœ ?€?…ì´ ?¬ìš©???•ì˜ Enum?¸ì? ?•ì¸?©ë‹ˆ??
    /// </summary>
    /// <param name="typeName">?•ì¸???€?…ëª…</param>
    /// <returns>?¬ìš©???•ì˜ Enum ?¬ë?</returns>
    public bool IsUserDefinedEnum(string typeName)
    {
        return _enumMap.ContainsKey(typeName);
    }

    /// <summary>
    /// Enum ë©¤ë²„ëª…ìœ¼ë¡?int ê°’ì„ ì¡°íšŒ?©ë‹ˆ??
    /// </summary>
    /// <param name="typeName">Enum ?€?…ëª…</param>
    /// <param name="memberName">ë©¤ë²„ëª?/param>
    /// <returns>ë©¤ë²„ ê°?(ì°¾ì? ëª»í•˜ë©?0)</returns>
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
    /// int ê°’ìœ¼ë¡?Enum ë©¤ë²„ëª…ì„ ì¡°íšŒ?©ë‹ˆ??
    /// </summary>
    /// <param name="typeName">Enum ?€?…ëª…</param>
    /// <param name="value">ë©¤ë²„ ê°?/param>
    /// <returns>ë©¤ë²„ëª?(ì°¾ì? ëª»í•˜ë©?ë¹?ë¬¸ì??</returns>
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
    /// ?¹ì • Enum ?€?…ì˜ ëª¨ë“  ë©¤ë²„ ?•ë³´ë¥?ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    /// <param name="typeName">Enum ?€?…ëª…</param>
    /// <returns>ë©¤ë²„ Dictionary (?†ìœ¼ë©?ë¹?Dictionary)</returns>
    public IReadOnlyDictionary<string, int> GetEnumMembers(string typeName)
    {
        if (_enumMap.TryGetValue(typeName, out var members))
        {
            return members;
        }
        return new Dictionary<string, int>();
    }

    /// <summary>
    /// ?±ë¡??ëª¨ë“  Enum ?€?…ëª…??ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    /// <returns>Enum ?€?…ëª… ëª©ë¡</returns>
    public IEnumerable<string> GetAllEnumTypes()
    {
        return _enumMap.Keys;
    }

    /// <summary>
    /// ?±ë¡??Enum ê°œìˆ˜ë¥?ë°˜í™˜?©ë‹ˆ??
    /// </summary>
    public int EnumCount => _enumMap.Count;

    /// <summary>
    /// ëª¨ë“  Enum ?°ì´?°ë? ì´ˆê¸°?”í•©?ˆë‹¤.
    /// </summary>
    public void Clear()
    {
        _enumMap.Clear();
        _reverseMap.Clear();
    }

    /// <summary>
    /// ?”ë²„ê·¸ìš©: ?„ì¬ ë¡œë“œ??Enum ?•ë³´ë¥?ì¶œë ¥?©ë‹ˆ??
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

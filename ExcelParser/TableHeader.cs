using TableAutomation.Common.Utils;
using ExcelParser.Core;

namespace ExcelParser;

/// <summary>
/// ?Œì´ë¸??¤ë” ?•ë³´ë¥?ê´€ë¦¬í•˜???´ë˜??/// </summary>
public class TableHeader
{
    public struct Group
    {
        public string name;
        public string typeString;
        public EDataType dataType;
        public string customTypeName;
        public bool isArray;
    }
    
    private Dictionary<int, Group> _headers = new Dictionary<int, Group>();

    public IEnumerable<Group> Headers => _headers.Values;
    public IEnumerable<string> Names => _headers.Values.Select(x => x.name);
    public IEnumerable<string> TypeStrings => _headers.Values.Select(x => x.typeString);
    public IEnumerable<int> ColumnIndices => _headers.Keys;
    public int ColumnCount => _headers.Count;
    public Group this[int idx] => _headers[idx];
    
    public string TableName { get; set; }

    /// <summary>
    /// ì¹¼ëŸ¼ ?€???•ì…”?ˆë¦¬ë¡œë???TableHeaderë¥??ì„±?©ë‹ˆ??
    /// </summary>
    /// <param name="tableName">?Œì´ë¸”ëª…</param>
    /// <param name="columnTypes">ì¹¼ëŸ¼ëª…ê³¼ ?€??ë¬¸ì???•ì…”?ˆë¦¬</param>
    public TableHeader(string tableName, Dictionary<string, string> columnTypes)
    {
        TableName = tableName;
        
        int index = 0;
        foreach (var kvp in columnTypes)
        {
            var (dataType, customTypeName, isArray) = DataTypeHelper.ParseDataType(kvp.Value);
            var typeString = DataTypeHelper.GetSystemTypeString(dataType, customTypeName, isArray);
            
            _headers.Add(index, new Group()
            {
                name = kvp.Key,
                typeString = typeString,
                dataType = dataType,
                customTypeName = customTypeName,
                isArray = isArray
            });
            index++;
        }
    }

    public bool HasColumn(string name)
    {
        return Names.Contains(name);
    }
    
    public string GetColumnTypeString(string columnName)
    {
        return Headers.ToList().Find(x => columnName.Equals(x.name)).typeString;
    }
    
    public EDataType GetColumnDataType(string columnName)
    {
        return Headers.ToList().Find(x => columnName.Equals(x.name)).dataType;
    }
    
    public string GetColumnCustomTypeName(string columnName)
    {
        return Headers.ToList().Find(x => columnName.Equals(x.name)).customTypeName;
    }
    
    public bool IsColumnArray(string columnName)
    {
        return Headers.ToList().Find(x => columnName.Equals(x.name)).isArray;
    }
}

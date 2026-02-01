using TableAutomation.Common.Utils;
using ExcelParser.Core;

namespace ExcelParser;

/// <summary>
/// 테이블 헤더 정보를 관리하는 클래스
/// </summary>
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
    /// 칼럼 타입 딕셔너리로부터 TableHeader를 생성합니다
    /// </summary>
    /// <param name="tableName">테이블명</param>
    /// <param name="columnTypes">칼럼명과 타입 문자열 딕셔너리</param>
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

using ClosedXML.Excel;
using Common.Utils;
using ExcelParser.Core;

namespace ExcelParser;

public class TableHeader
{
    public struct Group
    {
        public string name;
        public Type type;
        public EDataType dataType;
        public string customTypeName;
        public bool isArray;
    }
    
    private Dictionary<int, Group> _headers = new Dictionary<int, Group>();

    public IEnumerable<Group> Headers => _headers.Values;
    public IEnumerable<string> Names => _headers.Values.Select(x => x.name);
    public IEnumerable<Type> Types => _headers.Values.Select(x => x.type);
    public IEnumerable<int> ColumnIndices => _headers.Keys;
    public int ColumnCount => _headers.Count;
    public Group this[int idx] => _headers[idx];
    
    public int HeaderStartRow { get; private set; }
    public int HeaderEndRow { get; private set; }
    public int HeaderRowCount => HeaderEndRow - HeaderStartRow + 1;
    
    public string TableName { get; set; }

    /// <summary>
    /// Dictionary로부터 TableHeader를 생성합니다.
    /// </summary>
    public TableHeader(string tableName, Dictionary<string, Type> columnTypes)
    {
        TableName = tableName;
        HeaderStartRow = HeaderEndRow = 1;
        
        int index = 0;
        foreach (var kvp in columnTypes)
        {
            var (dataType, customTypeName, isArray) = DataTypeHelper.ParseDataType(kvp.Value.Name);
            
            _headers.Add(index, new Group()
            {
                name = kvp.Key,
                type = kvp.Value,
                dataType = dataType,
                customTypeName = customTypeName,
                isArray = isArray
            });
            index++;
        }
    }

    public TableHeader(IXLWorksheet sheet)
    {
        TableName = sheet.Name;
        
        var rows = TableHelper.GetRows(sheet, out var startIdx, out _);
        HeaderStartRow = HeaderEndRow = startIdx;
        
        if (rows.Count < 2)
            return;
            
        // 첫 번째 행은 칼럼 명
        var names = rows[0].Cells()?.ToList();
        if (names == null || names.Count == 0)
        {
            Logger.Instance.Error($"{sheet.Name} InValid Sheet. There is no columns (row : {HeaderEndRow})");
            return;
        }
        
        // 두 번째 행은 칼럼 타입
        HeaderEndRow++;
        var types = rows[1].Cells()?.ToList();
        if (types == null || types.Count == 0)
        {
            Logger.Instance.Error($"{sheet.Name} InValid Sheet. There is no columns (row : {HeaderEndRow})");
            return;
        }

        for (int i = 0; i < names.Count; i++)
        {
            if (types.Count <= i)
                break;
                
            var name = names[i]?.Value.ToString() ?? "";
            var typeStr = types[i]?.Value.ToString() ?? "";

            if (name.StartsWith(TableDefine.REGEX_START_EXCEPT_COLUMN))
                continue;
            
            if (string.IsNullOrEmpty(name))
            {
                Logger.Instance.Error($"{sheet.Name} Name is empty. (column : {i})");
                continue;
            }

            if (Names.Contains(name))
                continue;
            
            // DataTypeHelper를 사용하여 타입 파싱
            var (dataType, customTypeName, isArray) = DataTypeHelper.ParseDataType(typeStr);
            
            // 사용자 정의 Enum 검증
            if (dataType == EDataType.UserDefinedEnum && !DataTypeHelper.IsValidUserDefinedEnum(customTypeName))
            {
                Logger.Instance.Warning($"{sheet.Name} Unknown user-defined enum: {customTypeName} (column: {i}). Treating as int.");
            }
            
            // System.Type 결정
            var type = DataTypeHelper.GetSystemType(dataType, isArray);
            
            _headers.Add(i, new Group()
            {
                name = name,
                type = type,
                dataType = dataType,
                customTypeName = customTypeName,
                isArray = isArray
            });
        }
    }

    public bool HasColumn(string name)
    {
        return Names.Contains(name);
    }
    
    public Type GetColumnType(string columnName)
    {
        return Headers.ToList().Find(x => columnName.Equals(x.name)).type;
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

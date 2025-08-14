using ClosedXML.Excel;
using Common.Utils;

namespace ExcelParser.Core;

public class TableHeader
{
    public struct Group
    {
        public string name;
        public Type type;
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

    public TableHeader(IXLWorksheet sheet)
    {
        var rows = TableHelper.GetRows(sheet, out var startIdx, out _);
        HeaderStartRow = HeaderEndRow = startIdx;
        
        if (rows.Count < 2)
            return;
        // 첫 번째 행은 칼럼 명
        var names = rows[0].Cells()?.ToList();
        if (names == null)
        {
            Logger.Instance.Error($"{sheet.Name} InValid Sheet. There is no columns (row : {HeaderEndRow})");
            return;
        }
        if (names.Count == 0)
        {
            Logger.Instance.Error($"{sheet.Name} InValid Sheet. There is no columns (row : {HeaderEndRow})");
            return; 
        }
        // 두 번째 행은 칼럼 타입
        HeaderEndRow++;
        var types = rows[1].Cells()?.ToList();
        if (types == null)
        {
            Logger.Instance.Error($"{sheet.Name} InValid Sheet. There is no columns (row : {HeaderEndRow})");
            return;
        }
        if (types.Count == 0)
        {
            Logger.Instance.Error($"{sheet.Name} InValid Sheet. There is no columns (row : {HeaderEndRow})");
            return;
        }

        for (int i = 0; i < names.Count; i++)
        {
            if (types.Count <= i)
                break;
            var name = names[i]?.Value.ToString() ?? "";
            var typeStr = "System." + (types[i]?.Value.ToString() ?? "" );
            var type = Type.GetType(typeStr);

            if (!string.IsNullOrEmpty(name) && name.StartsWith(TableDefine.REGEX_START_EXCEPT_COLUMN))
            {
                Logger.Instance.Log($"{sheet.Name} Excluding commented column: '{name}' (column index: {i})");
                continue;
            }
            
            if ("".Equals(name))
            {
                Logger.Instance.Error($"{sheet.Name} Name is empty. (column : {i})");
                continue;
            }
            if (type == null)
            {
                Logger.Instance.Error($"{sheet.Name} Type is invalid. (column : {i}, type : {typeStr})");
                continue;
            }

            if(Names.Contains(name))
                continue;
            
            _headers.Add(i, new Group()
            {
                name = name,
                type = type,
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
}
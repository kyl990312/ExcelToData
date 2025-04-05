using System.Data;
using ClosedXML.Excel;
using Common.Utils;
using ExcelParser;
using System.Text;
using Common.Command;

namespace Generator.EnumGenerator;


public interface IEnumGenerator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="worksheet">이넘을 정의하는 테이블</param>
    /// <param name="dstDirectory">코드가 저장될 디렉토리 위치</param>
    /// <param name="codeExtension">*.extension</param>
    void Generate(IXLWorksheet worksheet, string dstDirectory, string codeExtension);
}

public class EnumGenerator : IEnumGenerator
{
    private const string EnumTypeColumn = "enumType";
    private const string MemberColumn = "member";
    private const string ValueColumn = "value";
    
    private Dictionary<string, List<(string member, int value)>> enumCache = new();
    public void Generate(IXLWorksheet worksheet, string dstDirectory, string codeExtension)
    {
        if (worksheet == null)
        {
            Logger.Instance.Log("[EnumGenerator] Worksheet is null]");
            return;
        }
        
        var table = TableHelper.GetTable(worksheet);
        if (table == null)
        {
            Logger.Instance.Log("[EnumGenerator] Failed Convert Worksheet To DataTable.");
        }
        
        CreateEnums(table);
        if (enumCache.Count == 0)
        {
            Logger.Instance.Log("[EnumGenerator] No Enums. File is Empty.");
        }

        var code = CreateCode();
        var fileName = Path.Combine(dstDirectory, $"{worksheet.Name}{codeExtension}");
        FileLoader.SaveText(code, fileName);
    }

    /// <summary>
    /// Generate 메서드 이후로 호출하면 Generate로 만들어진 enum의 type을 string 리스트로 반환합니다.
    /// 이넘 타입 목록이 필요한 경우 호출합니다.
    /// </summary>
    public string[] CreateEnumList()
    {
        return enumCache.Keys.ToArray();
    }

    private void CreateEnums(DataTable table)
    {
        enumCache.Clear();
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            string enumType = row[0]?.ToString() ?? string.Empty;
            string member = row[1]?.ToString() ?? string.Empty;
            int value = int.Parse(row[2]?.ToString() ?? "0");
            
            if(enumType == string.Empty)
                continue;
            
            if(!enumCache.ContainsKey(enumType))
                enumCache.Add(enumType, new List<(string member, int value)>());
            enumCache[enumType].Add((member, value));
        }
    }

    private string CreateCode()
    {
        StringBuilder codeBuilder = new StringBuilder();
    
        foreach (var enumType in enumCache.Keys)
        {
            codeBuilder.AppendLine($"public enum {enumType}");
            codeBuilder.AppendLine("{");
        
            // 값을 기준으로 정렬
            var sortedMembers = enumCache[enumType].OrderBy(x => x.value).ToList();
        
            for (int i = 0; i < sortedMembers.Count; i++)
            {
                var (member, value) = sortedMembers[i];
                string comma = (i < sortedMembers.Count - 1) ? "," : "";
                codeBuilder.AppendLine($"    {member} = {value}{comma}");
            }
        
            codeBuilder.AppendLine("}");
            codeBuilder.AppendLine();
        }
    
        string code = codeBuilder.ToString();
        return code;
    }
    
    /// <summary>
    /// 이넘 엑셀 파일을 생성합니다.
    /// </summary>
    /// <param name="dstDirectory"></param>
    public static void CreateEnumFile(string dstDirectory)
    {
        var sheetData = new SheetData()
        {
            SheetName = "SystemEnum",
            ColumnNames = [EnumTypeColumn, MemberColumn, ValueColumn],
            ColumnTypes = [typeof(string),typeof(string),typeof(int)],
            DataList = new List<object[]>(),
        };

        ExcelGenerator generator = new(new List<SheetData>(){sheetData}, dstDirectory);
        generator.SaveFile();
    }
}
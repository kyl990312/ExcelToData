using ClosedXML.Excel;
using Common.Utils;
using ExcelParser;
using System.Text;
using Common.Command;

namespace Generator.EnumGenerator;

public interface IEnumGenerator
{
    /// <summary>
    /// Enum을 정의하는 테이블에서 Enum 코드를 생성합니다.
    /// </summary>
    /// <param name="worksheet">Enum을 정의하는 워크시트</param>
    /// <param name="dstDirectory">코드가 저장될 디렉토리 위치</param>
    /// <param name="codeExtension">파일 확장자 (예: .cs)</param>
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
            Logger.Instance.Log("[EnumGenerator] Worksheet is null");
            return;
        }
        
        // CustomDataTable을 사용하여 데이터 추출
        var table = TableHelper.GetTable(worksheet);
        if (table == null)
        {
            Logger.Instance.Log("[EnumGenerator] Failed to convert Worksheet to CustomDataTable.");
            return;
        }
        
        CreateEnums(table);
        if (enumCache.Count == 0)
        {
            Logger.Instance.Log("[EnumGenerator] No Enums. File is Empty.");
            return;
        }

        var code = CreateCode();
        var fileName = Path.Combine(dstDirectory, $"{worksheet.Name}{codeExtension}");
        FileLoader.SaveText(code, fileName);
        
        Logger.Instance.Log($"[EnumGenerator] Generated {enumCache.Count} enums in {fileName}");
    }

    /// <summary>
    /// Generate 메서드 이후로 호출하면 Generate로 만들어진 enum의 type을 string 리스트로 반환합니다.
    /// 이넘 타입 목록이 필요한 경우 호출합니다.
    /// </summary>
    public string[] CreateEnumList()
    {
        return enumCache.Keys.ToArray();
    }

    /// <summary>
    /// CustomDataTable에서 Enum 데이터를 추출합니다.
    /// </summary>
    private void CreateEnums(CustomDataTable table)
    {
        enumCache.Clear();
        
        // 필수 컬럼 존재 여부 확인
        if (!table.HasColumn(EnumTypeColumn) || 
            !table.HasColumn(MemberColumn) || 
            !table.HasColumn(ValueColumn))
        {
            Logger.Instance.Error($"[EnumGenerator] Required columns missing. Need: {EnumTypeColumn}, {MemberColumn}, {ValueColumn}");
            return;
        }
        
        for (int i = 0; i < table.RowCount; i++)
        {
            try
            {
                var enumType = table.GetValue<string>(EnumTypeColumn, i);
                var member = table.GetValue<string>(MemberColumn, i);
                var valueStr = table.GetValue(ValueColumn, i)?.ToString();
                
                // 빈 데이터 스킵
                if (string.IsNullOrEmpty(enumType) || string.IsNullOrEmpty(member))
                {
                    Logger.Instance.Warning($"[EnumGenerator] Skipping row {i}: enumType or member is empty");
                    continue;
                }
                
                // value 파싱
                if (!int.TryParse(valueStr, out int value))
                {
                    Logger.Instance.Warning($"[EnumGenerator] Invalid value at row {i}: '{valueStr}', using 0");
                    value = 0;
                }
                
                // enumCache에 추가
                if (!enumCache.ContainsKey(enumType))
                    enumCache.Add(enumType, new List<(string member, int value)>());
                
                enumCache[enumType].Add((member, value));
                
                Logger.Instance.Log($"[EnumGenerator] Added {enumType}.{member} = {value}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"[EnumGenerator] Error processing row {i}: {ex.Message}");
            }
        }
        
        Logger.Instance.Log($"[EnumGenerator] Processed {enumCache.Count} enum types");
    }

    /// <summary>
    /// 캐시된 Enum 데이터로부터 C# 코드를 생성합니다.
    /// </summary>
    private string CreateCode()
    {
        StringBuilder codeBuilder = new StringBuilder();
        
        // 파일 헤더
        codeBuilder.AppendLine("// 자동 생성된 Enum 코드 - 수정하지 마세요");
        codeBuilder.AppendLine($"// 생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        codeBuilder.AppendLine();
        
        bool isFirst = true;
        foreach (var enumType in enumCache.Keys)
        {
            if (!isFirst)
                codeBuilder.AppendLine(); // Enum 사이에 빈 줄
            
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
            
            isFirst = false;
        }
    
        string code = codeBuilder.ToString();
        return code;
    }
    
    /// <summary>
    /// Enum 정의용 Excel 파일을 생성합니다.
    /// </summary>
    /// <param name="dstFilePath">생성할 Excel 파일의 전체 경로</param>
    public static void CreateEnumFile(string dstFilePath)
    {
        var sheetData = new SheetData()
        {
            SheetName = "SystemEnum",
            ColumnNames = [EnumTypeColumn, MemberColumn, ValueColumn],
            ColumnTypes = [typeof(string), typeof(string), typeof(int)],
            DataList = new List<object[]>()
            {
                // 예시 데이터
                new object[] { "ItemType", "Weapon", 1 },
                new object[] { "ItemType", "Armor", 2 },
                new object[] { "ItemType", "Consumable", 3 },
                new object[] { "ItemRarity", "Common", 0 },
                new object[] { "ItemRarity", "Rare", 1 },
                new object[] { "ItemRarity", "Epic", 2 },
            },
        };

        var generator = new ExcelGenerator(new List<SheetData>(){sheetData}, dstFilePath);
        generator.SaveFile();
        
        Logger.Instance.Log($"[EnumGenerator] Created enum template file: {dstFilePath}");
    }
    
    /// <summary>
    /// 디버그용: 현재 캐시된 Enum 정보를 출력합니다.
    /// </summary>
    public void PrintEnumInfo()
    {
        Logger.Instance.Log($"[EnumGenerator] Total enum types: {enumCache.Count}");
        
        foreach (var kvp in enumCache)
        {
            Logger.Instance.Log($"[EnumGenerator] {kvp.Key}: {kvp.Value.Count} members");
            foreach (var (member, value) in kvp.Value)
            {
                Logger.Instance.Log($"[EnumGenerator]   - {member} = {value}");
            }
        }
    }
}

using TableAutomationTool.Common.Utils;
using TableAutomationTool.ExcelParser;
using System.Text;
using TableAutomationTool.UserEnum;
using TableAutomationTool.Common.Command;

namespace TableAutomationTool.CodeGenerator;

public interface IEnumCodeGenerator
{
    /// <summary>
    /// EnumCache로부터 Enum 코드를 생성합니다.
    /// </summary>
    /// <param name="enumCache">Enum 데이터를 포함하는 EnumCache</param>
    /// <param name="dstDirectory">코드가 저장될 디렉토리 위치</param>
    /// <param name="codeExtension">파일 확장자 (예: .cs)</param>
    void Generate(EnumCache enumCache, string dstDirectory, string codeExtension);
}

public class EnumCodeGenerator : IEnumCodeGenerator
{
    private const string EnumTypeColumn = "type";
    private const string MemberColumn = "member";
    private const string ValueColumn = "value";

    /// <summary>
    /// 외부에서 전달받은 EnumCache를 기반으로 Enum 코드를 생성합니다.
    /// </summary>
    /// <param name="enumCache">Enum 데이터를 포함하는 EnumCache</param>
    /// <param name="dstDirectory">코드가 저장될 디렉토리 위치</param>
    /// <param name="codeExtension">파일 확장자 (예: .cs)</param>
    public void Generate(EnumCache enumCache, string dstDirectory, string codeExtension)
    {
        if (enumCache == null)
        {
            Logger.Instance.Error("[EnumCodeGenerator] EnumCache가 null입니다");
            return;
        }

        if (enumCache.IsEmpty)
        {
            Logger.Instance.Warning("[EnumCodeGenerator] EnumCache가 비어있습니다");
            return;
        }

        if (string.IsNullOrEmpty(dstDirectory))
        {
            Logger.Instance.Error("[EnumCodeGenerator] 출력 디렉토리가 비어있습니다");
            return;
        }

        // 디렉토리 생성
        if (!Directory.Exists(dstDirectory))
        {
            Directory.CreateDirectory(dstDirectory);
        }

        // 시트별로 코드 파일 생성
        foreach (var sheetName in enumCache.SheetNames)
        {
            var code = CreateCode(enumCache, sheetName);
            var fileName = Path.Combine(dstDirectory, $"{sheetName}{codeExtension}");
            FileLoader.SaveText(code, fileName);

            var enumCount = enumCache.GetEnumTypesBySheet(sheetName).Count;
            Logger.Instance.Log($"[EnumCodeGenerator] Generated {enumCount} enums in {fileName}");
        }
    }

    /// <summary>
    /// 캐시된 Enum 데이터로부터 C# 코드를 생성합니다.
    /// </summary>
    private string CreateCode(EnumCache enumCache, string sheetName)
    {
        var enumTypes = enumCache.GetEnumTypesBySheet(sheetName);
        
        StringBuilder codeBuilder = new();
        
        // 파일 헤더
        codeBuilder.AppendLine("// 자동 생성된 Enum 코드 - 수정하지 마세요");
        codeBuilder.AppendLine($"// 생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        codeBuilder.AppendLine();
        
        bool isFirst = true;
        foreach (var enumType in enumTypes)
        {
            if (!isFirst)
                codeBuilder.AppendLine();
            
            codeBuilder.AppendLine($"public enum {enumType}");
            codeBuilder.AppendLine("{");
        
            var members = enumCache.GetEnumMembers(enumType);
            for (int i = 0; i < members.Count; i++)
            {
                var (member, value) = members[i];
                string comma = (i < members.Count - 1) ? "," : "";
                codeBuilder.AppendLine($"    {member} = {value}{comma}");
            }
        
            codeBuilder.AppendLine("}");
            
            isFirst = false;
        }
    
        return codeBuilder.ToString();
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
            DataList =
            [
                ["ItemType", "Weapon", 1],
                ["ItemType", "Armor", 2],
                ["ItemType", "Consumable", 3],
                ["ItemRarity", "Common", 0],
                ["ItemRarity", "Rare", 1],
                ["ItemRarity", "Epic", 2],
            ],
        };

        var generator = new ExcelGenerator([sheetData], dstFilePath);
        generator.SaveFile();
        
        Logger.Instance.Log($"[EnumCodeGenerator] Created enum template file: {dstFilePath}");
    }
}

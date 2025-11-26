using Common.Command;
using Common.Utils;
using ClosedXML.Excel;
using Exception = System.Exception;

namespace EnumGenerator;

public class CreateEnumGenerateConfigCommand : Command
{
    private string _path;
    public CreateEnumGenerateConfigCommand(string path)
    {
        _path = path;
    }
    
    public override void Execute(out string resultMsg)
    {
        resultMsg = string.Empty;
        var config = EnumGeneratorConfig.GetDefault();
        if (FileLoader.SaveJson(_path, config))
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Success Create Config : {_path}";
        }
        else
        {
            resultMsg = $"[CreateEnumGenerateConfigCommand] Fail to Create Config : {_path}";
        }
    }
}

/// <summary>
/// Enum 코드(.cs)를 생성하는 커맨드
/// </summary>
public class GenerateEnumCodeCommand : Command
{
    private string _enumFilePath;
    private string _directoryPath;
    private string _codeExtension;

    public GenerateEnumCodeCommand(string enumFilePath, string directoryPath, string codeExtension)
    {
        _enumFilePath = enumFilePath;
        _directoryPath = directoryPath;
        _codeExtension = codeExtension;
    }
    
    public override void Execute(out string result)
    {
        result = "";
        try
        {
            var enumGenerator = new Generator.EnumGenerator.EnumGenerator();
            using (var workbook = new XLWorkbook(_enumFilePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    enumGenerator.Generate(worksheet, _directoryPath, _codeExtension);
                    var enums = enumGenerator.CreateEnumList();
                    Logger.Instance.Log($"[GenerateEnumCodeCommand] Create Enums from {_enumFilePath}>{worksheet.Name} : {string.Join(", ", enums)}");
                }
            }
            result = $"[GenerateEnumCodeCommand] Complete Create Enums {_enumFilePath}";
        }
        catch (Exception e)
        {
            Logger.Instance.Log($"[GenerateEnumCodeCommand] Failed Execute Command\n{e.Message}");
        }
    }
}

/// <summary>
/// EnumDB 바이너리 파일을 생성하는 커맨드
/// </summary>
public class GenerateEnumDBCommand : Command
{
    private string _enumFilePath;
    private string _enumDBPath;

    public GenerateEnumDBCommand(string enumFilePath, string enumDBPath)
    {
        _enumFilePath = enumFilePath;
        _enumDBPath = enumDBPath;
    }
    
    public override void Execute(out string result)
    {
        result = "";
        try
        {
            var enumData = new Dictionary<string, Dictionary<string, int>>();
            
            using (var workbook = new XLWorkbook(_enumFilePath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    ParseEnumSheet(worksheet, enumData);
                }
            }
            
            if (enumData.Count == 0)
            {
                result = $"[GenerateEnumDBCommand] No enum data found in {_enumFilePath}";
                return;
            }
            
            SaveEnumDB(enumData, _enumDBPath);
            result = $"[GenerateEnumDBCommand] Complete Create EnumDB {_enumDBPath} ({enumData.Count} enums)";
        }
        catch (Exception e)
        {
            result = $"[GenerateEnumDBCommand] Failed Execute Command: {e.Message}";
            Logger.Instance.Error($"[GenerateEnumDBCommand] Failed Execute Command\n{e.Message}\n{e.StackTrace}");
        }
    }

    private void ParseEnumSheet(IXLWorksheet worksheet, Dictionary<string, Dictionary<string, int>> enumData)
    {
        const string EnumTypeColumn = "enumType";
        const string MemberColumn = "member";
        const string ValueColumn = "value";
        
        // 컬럼 인덱스 찾기
        var headerRow = worksheet.Row(1);
        int enumTypeIdx = -1, memberIdx = -1, valueIdx = -1;
        
        foreach (var cell in headerRow.CellsUsed())
        {
            var columnName = cell.Value.ToString();
            if (columnName == EnumTypeColumn) enumTypeIdx = cell.Address.ColumnNumber;
            else if (columnName == MemberColumn) memberIdx = cell.Address.ColumnNumber;
            else if (columnName == ValueColumn) valueIdx = cell.Address.ColumnNumber;
        }
        
        if (enumTypeIdx == -1 || memberIdx == -1 || valueIdx == -1)
        {
            Logger.Instance.Warning($"[GenerateEnumDBCommand] Required columns not found in {worksheet.Name}");
            return;
        }
        
        // 3행부터 데이터 (1행: 컬럼명, 2행: 타입)
        var rows = worksheet.RowsUsed().Skip(2);
        
        foreach (var row in rows)
        {
            var enumType = row.Cell(enumTypeIdx).Value.ToString();
            var member = row.Cell(memberIdx).Value.ToString();
            var valueStr = row.Cell(valueIdx).Value.ToString();
            
            // 빈 데이터 또는 EOF 스킵
            if (string.IsNullOrEmpty(enumType) || string.IsNullOrEmpty(member))
                continue;
            if (enumType.ToLower() == "eof")
                break;
            
            if (!int.TryParse(valueStr, out int value))
            {
                Logger.Instance.Warning($"[GenerateEnumDBCommand] Invalid value '{valueStr}' for {enumType}.{member}, using 0");
                value = 0;
            }
            
            if (!enumData.ContainsKey(enumType))
                enumData[enumType] = new Dictionary<string, int>();
            
            enumData[enumType][member] = value;
        }
    }

    private void SaveEnumDB(Dictionary<string, Dictionary<string, int>> enumData, string filePath)
    {
        // 디렉토리 생성
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream, System.Text.Encoding.UTF8);

        // Header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("ENDB")); // Magic Number (4 bytes)
        writer.Write(1); // Version (4 bytes)
        writer.Write(enumData.Count); // EnumCount (4 bytes)

        // Enum Entries
        foreach (var enumEntry in enumData)
        {
            // Enum Name
            writer.Write(enumEntry.Key);
            
            // Member Count
            writer.Write(enumEntry.Value.Count);
            
            // Member Entries
            foreach (var memberEntry in enumEntry.Value)
            {
                writer.Write(memberEntry.Key);   // Member Name
                writer.Write(memberEntry.Value); // Member Value
            }
        }

        Logger.Instance.Log($"[GenerateEnumDBCommand] Saved EnumDB to {filePath}");
    }
}

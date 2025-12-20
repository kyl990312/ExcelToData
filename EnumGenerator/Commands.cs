using Common.Command;
using Common.Utils;
using ClosedXML.Excel;
using ExcelParser;
using GAuth;
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
/// 로컬 Enum 테이블에서 EnumCache를 생성하는 커맨드
/// </summary>
public class EnumCacheGenerateCommand : Command
{
    private readonly string _enumFilePath;

    public EnumCacheGenerateCommand(string enumFilePath)
    {
        _enumFilePath = enumFilePath;
    }

    public override void Execute(out string result)
    {
        result = "";
        
        if (string.IsNullOrEmpty(_enumFilePath))
        {
            result = "[EnumCacheGenerateCommand] Enum 파일 경로가 비어있습니다";
            Logger.Instance.Error(result);
            return;
        }

        if (!File.Exists(_enumFilePath))
        {
            result = $"[EnumCacheGenerateCommand] Enum 파일을 찾을 수 없습니다: {_enumFilePath}";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            SharedData.Instance.EnumCache.Clear();

            using var workbook = new XLWorkbook(_enumFilePath);
            foreach (var worksheet in workbook.Worksheets)
            {
                var table = TableHelper.GetTable(worksheet);
                if (table == null)
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateCommand] 테이블 변환 실패: {worksheet.Name}");
                    continue;
                }

                if (!SharedData.Instance.EnumCache.LoadFromTable(table))
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateCommand] EnumCache 로드 실패: {worksheet.Name}");
                    continue;
                }

                Logger.Instance.Log($"[EnumCacheGenerateCommand] EnumCache 로드 완료: {worksheet.Name}");
            }

            var enumCount = SharedData.Instance.EnumCache.EnumCount;
            var sheetCount = SharedData.Instance.EnumCache.SheetCount;
            result = $"[EnumCacheGenerateCommand] EnumCache 생성 완료: {enumCount}개 Enum 타입, {sheetCount}개 시트";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[EnumCacheGenerateCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// 구글 시트에서 EnumCache를 생성하는 커맨드
/// </summary>
public class EnumCacheGenerateFromGSheetCommand : Command
{
    private readonly string _gDriveLink;
    private readonly string[] _sheetPatterns;

    public EnumCacheGenerateFromGSheetCommand(string gDriveLink, string[] sheetPatterns = null)
    {
        _gDriveLink = gDriveLink;
        _sheetPatterns = sheetPatterns ?? ["*Enum*", "*enum*"];
    }

    public override void Execute(out string result)
    {
        result = "";

        if (string.IsNullOrEmpty(_gDriveLink))
        {
            result = "[EnumCacheGenerateFromGSheetCommand] 구글 드라이브 링크가 비어있습니다";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            Logger.Instance.Log($"[EnumCacheGenerateFromGSheetCommand] 구글 시트에서 Enum 로드 시작: {_gDriveLink}");

            SharedData.Instance.EnumCache.Clear();

            // 구글 시트에서 DataTable 목록 가져오기
            var dataTables = GAuthService.GetTableFromGSheet(_gDriveLink);
            
            if (dataTables == null || dataTables.Count == 0)
            {
                result = "[EnumCacheGenerateFromGSheetCommand] 구글 시트에서 데이터를 찾을 수 없습니다";
                Logger.Instance.Warning(result);
                return;
            }

            int loadedCount = 0;
            foreach (var dataTable in dataTables)
            {
                // 패턴에 맞는 시트만 처리
                if (!IsMatchPattern(dataTable.TableName))
                {
                    Logger.Instance.Log($"[EnumCacheGenerateFromGSheetCommand] 패턴 불일치로 스킵: {dataTable.TableName}");
                    continue;
                }

                // TableHelper를 사용하여 CustomDataTable로 변환 (헤더 자동 처리)
                var customTable = TableHelper.GetTable(dataTable);
                if (customTable == null)
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateFromGSheetCommand] CustomDataTable 변환 실패: {dataTable.TableName}");
                    continue;
                }

                // 테이블 이름에서 파일명 prefix 제거 (예: "FileName_SheetName" -> "SheetName")
                var nameStrs = customTable.Name.Split('_');
                if (nameStrs.Length > 1)
                {
                    customTable.Name = customTable.Name.Replace(nameStrs[0] + "_", "");
                }

                if (!SharedData.Instance.EnumCache.LoadFromTable(customTable))
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateFromGSheetCommand] EnumCache 로드 실패: {customTable.Name}");
                    continue;
                }

                loadedCount++;
                Logger.Instance.Log($"[EnumCacheGenerateFromGSheetCommand] EnumCache 로드 완료: {customTable.Name}");
            }

            var enumCount = SharedData.Instance.EnumCache.EnumCount;
            var sheetCount = SharedData.Instance.EnumCache.SheetCount;
            result = $"[EnumCacheGenerateFromGSheetCommand] 구글 시트에서 EnumCache 생성 완료: {enumCount}개 Enum 타입, {sheetCount}개 시트 (총 {loadedCount}개 시트 처리)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[EnumCacheGenerateFromGSheetCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 테이블 이름이 패턴과 일치하는지 확인합니다.
    /// </summary>
    private bool IsMatchPattern(string tableName)
    {
        if (_sheetPatterns == null || _sheetPatterns.Length == 0)
            return true;

        foreach (var pattern in _sheetPatterns)
        {
            var searchTerm = pattern.Replace("*", "");
            if (tableName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Enum 코드(.cs)를 생성하는 커맨드
/// EnumCacheGenerateCommand가 먼저 실행되어 SharedData.EnumCache가 채워져 있어야 합니다.
/// </summary>
public class GenerateEnumCodeCommand : Command
{
    private readonly string _directoryPath;
    private readonly string _codeExtension;

    public GenerateEnumCodeCommand(string directoryPath, string codeExtension)
    {
        _directoryPath = directoryPath;
        _codeExtension = codeExtension;
    }
    
    public override void Execute(out string result)
    {
        result = "";
        
        if (SharedData.Instance.EnumCache.IsEmpty)
        {
            result = "[GenerateEnumCodeCommand] EnumCache가 비어있습니다. EnumCacheGenerateCommand를 먼저 실행하세요.";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            var enumGenerator = new Generator.EnumGenerator.EnumCodeGenerator();
            enumGenerator.Generate(SharedData.Instance.EnumCache, _directoryPath, _codeExtension);
            
            var enums = SharedData.Instance.EnumCache.EnumTypes.ToArray();
            Logger.Instance.Log($"[GenerateEnumCodeCommand] 생성된 Enum 목록: {string.Join(", ", enums)}");
            
            result = $"[GenerateEnumCodeCommand] Enum 코드 생성 완료: {_directoryPath}";
        }
        catch (Exception e)
        {
            result = $"[GenerateEnumCodeCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// EnumDB 바이너리 파일을 생성하는 커맨드
/// EnumCacheGenerateCommand가 먼저 실행되어 SharedData.EnumCache가 채워져 있어야 합니다.
/// </summary>
public class GenerateEnumBinaryCommand : Command
{
    private readonly string _enumDBPath;

    public GenerateEnumBinaryCommand(string enumDBPath)
    {
        _enumDBPath = enumDBPath;
    }
    
    public override void Execute(out string result)
    {
        result = "";
        
        if (SharedData.Instance.EnumCache.IsEmpty)
        {
            result = "[GenerateEnumBinaryCommand] EnumCache가 비어있습니다. EnumCacheGenerateCommand를 먼저 실행하세요.";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            SharedData.Instance.EnumCache.SaveToBinary(_enumDBPath);
            result = $"[GenerateEnumBinaryCommand] EnumDB 생성 완료: {_enumDBPath} ({SharedData.Instance.EnumCache.EnumCount}개 Enum)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[GenerateEnumBinaryCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

using TableAutomation.Common;
using TableAutomation.Common.Command;
using TableAutomation.Common.Utils;
using ClosedXML.Excel;
using ExcelParser;
using GAuth;
using System.Text;
using UserEnum;
using Exception = System.Exception;
using TableAutomarionToolShared;

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
            EnumDB.Instance.Clear();

            using var workbook = new XLWorkbook(_enumFilePath);
            foreach (var worksheet in workbook.Worksheets)
            {
                var table = TableHelper.GetTable(worksheet);
                if (table == null)
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateCommand] 테이블 변환 실패: {worksheet.Name}");
                    continue;
                }

                if (!EnumDB.Instance.Cache.LoadFromTable(table))
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateCommand] EnumCache 로드 실패: {worksheet.Name}");
                    continue;
                }

                Logger.Instance.Log($"[EnumCacheGenerateCommand] EnumCache 로드 완료: {worksheet.Name}");
            }

            var enumCount = EnumDB.Instance.EnumCount;
            var sheetCount = EnumDB.Instance.Cache.SheetCount;
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

            EnumDB.Instance.Clear();

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
                var customTable = TableHelper.GetTable(dataTable.TableName, dataTable.Data);
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

                if (!EnumDB.Instance.Cache.LoadFromTable(customTable))
                {
                    Logger.Instance.Warning($"[EnumCacheGenerateFromGSheetCommand] EnumCache 로드 실패: {customTable.Name}");
                    continue;
                }

                loadedCount++;
                Logger.Instance.Log($"[EnumCacheGenerateFromGSheetCommand] EnumCache 로드 완료: {customTable.Name}");
            }

            var enumCount = EnumDB.Instance.EnumCount;
            var sheetCount = EnumDB.Instance.Cache.SheetCount;
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
/// EnumDB 테스트 커맨드 (Development 모드에서만 동작)
/// EnumDB의 모든 데이터를 콘솔에 출력하고 txt 파일로 저장합니다.
/// </summary>
public class EnumDBTestCommand : Command
{
    private readonly string _filePath;

    public EnumDBTestCommand(string filePath)
    {
        _filePath = filePath;
    }

    public override void Execute(out string result)
    {
        result = "";

        try
        {
            var sb = new StringBuilder();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            sb.AppendLine("========================================");
            sb.AppendLine($"[EnumDB Test Report]");
            sb.AppendLine($"생성 시간: {timestamp}");
            sb.AppendLine("========================================");
            sb.AppendLine();

            if (EnumDB.Instance.IsEmpty)
            {
                sb.AppendLine("EnumDB가 비어있습니다.");
            }
            else
            {
                sb.AppendLine($"총 시트: {EnumDB.Instance.Cache.SheetCount}개");
                sb.AppendLine($"총 Enum 타입: {EnumDB.Instance.EnumCount}개");
                sb.AppendLine();

                foreach (var sheetName in EnumDB.Instance.Cache.SheetNames)
                {
                    sb.AppendLine($"[시트: {sheetName}]");
                    var enumTypes = EnumDB.Instance.GetEnumTypesBySheet(sheetName);
                    
                    foreach (var enumType in enumTypes)
                    {
                        sb.AppendLine($"  └─ {enumType}");
                        var members = EnumDB.Instance.GetEnumMembers(enumType);
                        
                        foreach (var (member, value) in members)
                        {
                            sb.AppendLine($"       {member} = {value}");
                        }
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("========================================");

            var content = sb.ToString();

            // 콘솔 출력
            Console.WriteLine(content);

            // 파일 저장
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, content);

            result = $"[EnumDBTestCommand] - 테스트 완료. 저장 경로: {Path.GetFullPath(_filePath)}";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[EnumDBTestCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// EnumDB 바이너리 파일을 로드하는 커맨드 (Development 모드에서만 동작)
/// 저장된 EnumDB.bin을 다시 로드하여 정상 저장 여부를 검증합니다.
/// </summary>
public class EnumDBLoadCommand : Command
{
    public override void Execute(out string result)
    {
        result = "";

        var enumDBPath = SystemPath.EnumBinaryPath;

        if (!File.Exists(enumDBPath))
        {
            result = $"[EnumDBLoadCommand] EnumDB 파일을 찾을 수 없습니다: {enumDBPath}";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            // 기존 캐시 클리어 후 바이너리에서 로드
            EnumDB.Instance.Clear();
            
            if (!EnumDB.Instance.LoadFromBinary(enumDBPath))
            {
                result = $"[EnumDBLoadCommand] EnumDB 로드 실패: {enumDBPath}";
                Logger.Instance.Error(result);
                return;
            }

            var enumCount = EnumDB.Instance.EnumCount;
            var sheetCount = EnumDB.Instance.Cache.SheetCount;
            result = $"[EnumDBLoadCommand] EnumDB 로드 완료: {enumDBPath} ({enumCount}개 Enum 타입, {sheetCount}개 시트)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[EnumDBLoadCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

/// <summary>
/// EnumDB 바이너리 파일을 생성하는 커맨드
/// 시스템 통일 경로(SystemPath.EnumBinaryPath)에 저장합니다.
/// </summary>
public class GenerateEnumBinaryCommand : Command
{
    public override void Execute(out string result)
    {
        result = "";
        
        if (EnumDB.Instance.IsEmpty)
        {
            result = "[GenerateEnumBinaryCommand] EnumDB가 비어있습니다.";
            Logger.Instance.Error(result);
            return;
        }

        try
        {
            var enumDBPath = SystemPath.EnumBinaryPath;
            
            // 디렉토리 생성
            var directory = Path.GetDirectoryName(enumDBPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            EnumDB.Instance.SaveToBinary(enumDBPath);
            result = $"[GenerateEnumBinaryCommand] EnumDB 생성 완료: {enumDBPath} ({EnumDB.Instance.EnumCount}개 Enum)";
            Logger.Instance.Log(result);
        }
        catch (Exception e)
        {
            result = $"[GenerateEnumBinaryCommand] 실행 실패: {e.Message}";
            Logger.Instance.Error($"{result}\n{e.StackTrace}");
        }
    }
}

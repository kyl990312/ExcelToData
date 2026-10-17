using System.Data;
using TableAutomationTool.Common.Utils;
using TableAutomationTool.GAuth.Core;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace TableAutomationTool.GAuth;

public struct GSheetTable
{
    public string TableName { get; set; }
    public List<string[]> Data { get; set; }
    public string SpreadsheetId { get; set; }
    public string SheetTitle { get; set; }
}

/// <summary>
/// 데이터 없이 탭 메타데이터만 담는다. 목록 조회에서 전체 데이터를 내려받지 않기 위해 쓴다.
/// </summary>
public struct GSheetTab
{
    public string SpreadsheetName { get; set; }
    public string SpreadsheetId { get; set; }
    public string SheetTitle { get; set; }

    /// <summary>추출 결과 .bin 이름과 같은 테이블 이름 (ToTableName 규칙)</summary>
    public string TableName { get; set; }
}

public static class GAuthService
{
    /// <summary>
    /// 구글 드라이브 폴더 내의 모든 구글 시트를 읽어 GSheetTable로 생성합니다.
    /// 하나의 시트에 여러 탭이 존재하는 경우 각각 하나의 테이블이 됩니다.
    /// 테이블의 이름(시트의 탭)이 #이나 //로 시작하는 경우 테이블 생성에서 제외됩니다.
    /// </summary>
    /// <param name="folderLink">구글 드라이브 폴더 경로</param>
    /// <returns>폴더 내에 존재하는 구글 시트 파일이 갖는 모든 테이블</returns>
    /// <summary>
    /// Google 인증을 수행하고(필요하면 브라우저 로그인) 로그인한 계정 이메일을 돌려준다.
    /// </summary>
    public static string Authorize(UserCredentialConfig config, CancellationToken cancellationToken)
        => GAuthAPIHandler.Authorize(config, cancellationToken);

    public static List<GSheetTable> GetTableFromGSheet(string folderLink)
    {
        List<GSheetTable> tables = new();
        
        try
        {
            // 폴더 내의 모든 Google Sheets 파일 가져오기
            var googleSheets = GetAllGoogleSheets(folderLink);
            Logger.Instance.Log($"[GAuthService] Found {googleSheets.Count} Google Sheets");
            
            var sheetsService = GAuthAPIHandler.GetSheetsService();
            
            foreach (var sheetFile in googleSheets)
            {
                try
                {
                    Logger.Instance.Log($"[GAuthService] Processing sheet: {sheetFile.Name}");
                    
                    // 시트의 모든 탭 정보 가져오기
                    var spreadsheet = sheetsService.Spreadsheets.Get(sheetFile.Id).Execute();
                    
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        var sheetTitle = sheet.Properties.Title;
                        
                        // 제외 탭 필터링 (# 또는 //로 시작하는 경우)
                        if (IsExcludedTab(sheetTitle))
                        {
                            Logger.Instance.Log($"[GAuthService] Skipping excluded tab: {sheetTitle}");
                            continue;
                        }
                        
                        Logger.Instance.Log($"[GAuthService] Converting sheet tab to GSheetTable: {sheetTitle}");
                        
                        // 시트 데이터 읽기
                        var range = $"{sheetTitle}!A:Z"; // A부터 Z열까지 모든 행
                        var request = sheetsService.Spreadsheets.Values.Get(sheetFile.Id, range);
                        var response = request.Execute();
                        
                        if (response.Values != null && response.Values.Count > 0)
                        {
                            var gSheetTable = ConvertToGSheetTable(response.Values, $"{sheetFile.Name}_{sheetTitle}", sheetFile.Id, sheetTitle);
                            if (gSheetTable.Data != null && gSheetTable.Data.Count > 0)
                            {
                                tables.Add(gSheetTable);
                                Logger.Instance.Log($"[GAuthService] Successfully created GSheetTable: {gSheetTable.TableName} ({gSheetTable.Data.Count} rows)");
                            }
                        }
                        else
                        {
                            Logger.Instance.Warning($"[GAuthService] No data found in sheet tab: {sheetTitle}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"[GAuthService] Error processing sheet {sheetFile.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[GAuthService] Error in GetTableFromGSheet: {ex.Message}\n{ex.StackTrace}");
        }
        
        Logger.Instance.Log($"[GAuthService] Total GSheetTables created: {tables.Count}");
        return tables;
    }

    /// <summary>
    /// 폴더 내 구글 시트의 탭 목록만 가져온다(셀 데이터는 받지 않는다).
    /// 제외 규칙은 GetTableFromGSheet 와 같다. 오류는 삼키지 않고 호출자에게 던진다.
    /// </summary>
    public static List<GSheetTab> GetTabsFromGSheet(string folderLink)
    {
        var tabs = new List<GSheetTab>();
        var sheetsService = GAuthAPIHandler.GetSheetsService();

        foreach (var sheetFile in GetAllGoogleSheets(folderLink))
        {
            var request = sheetsService.Spreadsheets.Get(sheetFile.Id);
            request.Fields = "sheets.properties.title";
            var spreadsheet = request.Execute();

            foreach (var sheet in spreadsheet.Sheets)
            {
                var sheetTitle = sheet.Properties.Title;
                if (IsExcludedTab(sheetTitle))
                    continue;

                tabs.Add(new GSheetTab
                {
                    SpreadsheetName = sheetFile.Name,
                    SpreadsheetId = sheetFile.Id,
                    SheetTitle = sheetTitle,
                    TableName = ToTableName($"{sheetFile.Name}_{sheetTitle}"),
                });
            }
        }

        return tabs;
    }

    /// <summary>
    /// 탭 하나의 데이터만 읽어 GSheetTable 로 만든다. 범위·행 처리 규칙은 GetTableFromGSheet 와 같다.
    /// </summary>
    public static GSheetTable GetTableFromGSheetTab(GSheetTab tab)
    {
        var sheetsService = GAuthAPIHandler.GetSheetsService();
        var response = sheetsService.Spreadsheets.Values.Get(tab.SpreadsheetId, $"{tab.SheetTitle}!A:Z").Execute();
        if (response.Values == null || response.Values.Count == 0)
        {
            return new GSheetTable
            {
                TableName = tab.TableName,
                Data = new List<string[]>(),
                SpreadsheetId = tab.SpreadsheetId,
                SheetTitle = tab.SheetTitle
            };
        }

        return ConvertToGSheetTable(response.Values, tab.TableName, tab.SpreadsheetId, tab.SheetTitle);
    }

    /// <summary>
    /// GetTableFromGSheet 의 테이블 이름("{시트파일}_{탭}")을 추출 결과 .bin 이름으로 바꾼다.
    /// 첫 '_' 앞 토막과 '_' 를 지운다. BytesExtractorFromGSheet 가 .bin 이름을 정할 때 쓰는 규칙이다.
    /// </summary>
    public static string ToTableName(string gsheetTableName)
    {
        var head = gsheetTableName.Split('_')[0];
        return gsheetTableName.Replace(head + "_", "");
    }

    /// <summary>
    /// 캐시된 OAuth 토큰이 있는지 확인한다. 없으면 GSheet 호출이 브라우저 로그인을 기다리며 멈춘다.
    /// </summary>
    public static bool HasCachedToken() => GAuthAPIHandler.HasCachedToken();

    private static bool IsExcludedTab(string sheetTitle)
        => sheetTitle.StartsWith("#") || sheetTitle.StartsWith("//");

    /// <summary>
    /// Google Sheets의 값들을 GSheetTable로 변환합니다.
    /// </summary>
    /// <param name="values">Google Sheets에서 읽어온 값들</param>
    /// <param name="tableName">생성할 테이블의 이름</param>
    /// <returns>변환된 GSheetTable</returns>
    private static GSheetTable ConvertToGSheetTable(
        IList<IList<object>> values,
        string tableName,
        string spreadsheetId,
        string sheetTitle)
    {
        var table = new GSheetTable
        {
            TableName = tableName,
            Data = new List<string[]>(),
            SpreadsheetId = spreadsheetId,
            SheetTitle = sheetTitle
        };
        
        if (values == null || values.Count == 0)
        {
            Logger.Instance.Warning($"[GAuthService] No values to convert for table: {tableName}");
            return table;
        }
        
        try
        {
            // 모든 행을 string[]로 변환
            foreach (var row in values)
            {
                // 빈 행 체크
                if (row.All(cell => string.IsNullOrWhiteSpace(cell?.ToString())))
                {
                    continue;
                }
                
                // EOF 마커 확인
                if (row.Count > 0 && row[0]?.ToString()?.Equals("EOF", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Logger.Instance.Log($"[GAuthService] Reached EOF marker in {tableName}");
                    break;
                }
                
                var rowData = new string[row.Count];
                for (int i = 0; i < row.Count; i++)
                {
                    rowData[i] = row[i]?.ToString() ?? string.Empty;
                }
                
                table.Data.Add(rowData);
            }
            
            Logger.Instance.Log($"[GAuthService] GSheetTable created: {tableName} with {table.Data.Count} rows");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[GAuthService] Error converting to GSheetTable: {ex.Message}\n{ex.StackTrace}");
        }
        
        return table;
    }

    /// <summary>
    /// GSheet의 지정 탭을 rows로 덮어씁니다. clear → update 순서로 완전 갱신합니다.
    /// </summary>
    public static void UpdateGSheetTable(string spreadsheetId, string sheetTitle, List<string[]> rows)
    {
        var sheetsService = GAuthAPIHandler.GetSheetsService();
        var clearRange = $"{sheetTitle}!A:Z";

        try
        {
            var clearRequest = sheetsService.Spreadsheets.Values.Clear(
                new ClearValuesRequest(), spreadsheetId, clearRange);
            clearRequest.Execute();
        }
        catch (Exception ex)
        {
            throw new Exception($"GSheet 클리어 실패 (데이터 유지됨): {ex.Message}", ex);
        }

        var ilist = rows
            .Select(r => (IList<object>)r.Select(c => (object)c).ToList())
            .ToList<IList<object>>();
        var body = new ValueRange { Values = ilist };
        var updateRequest = sheetsService.Spreadsheets.Values.Update(
            body, spreadsheetId, $"{sheetTitle}!A1");
        updateRequest.ValueInputOption =
            SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        try
        {
            updateRequest.Execute();
        }
        catch (Exception ex)
        {
            throw new Exception($"GSheet 업데이트 실패 (GSheet가 비어 있습니다 — 재저장하세요): {ex.Message}", ex);
        }

        Logger.Instance.Log($"[GAuthService] UpdateGSheetTable 완료: {spreadsheetId} [{sheetTitle}] ({rows.Count} rows)");
    }

    /// <summary>
    /// 구글 드라이브에 존재하는 파일을 읽어 리스트로 반환합니다.
    /// </summary>
    /// <param name="folderLink">검색할 폴더를 지정합니다.</param>
    /// <param name="searchPatterns">검색할 파일명 패턴 배열. null인 경우 폴더내 모든 파일을 읽어옵니다.</param>
    /// <returns></returns>
    public static List<Google.Apis.Drive.v3.Data.File> GetAllFiles(string folderLink, string[] searchPatterns = null)
    {
        var service = GAuthAPIHandler.GetDriveService();
        // 폴더 링크에서 폴더 ID 추출
        string folderId = ExtractFolderId(folderLink);
        Logger.Instance.Log($"[GAuthService] Get Files from : {folderLink} -> {folderId}");

        // 기본 쿼리: 폴더 내의 모든 파일 (휴지통 제외)
        string baseQuery = $"'{folderId}' in parents and trashed=false";

        // searchPatterns가 지정된 경우 파일명 필터 추가
        if (searchPatterns != null && searchPatterns.Length > 0)
        {
            var nameConditions = new List<string>();
            foreach (var pattern in searchPatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    // 와일드카드 처리: *를 포함한 패턴을 contains로 변환
                    if (pattern.Contains("*"))
                    {
                        // 예: "Player*" -> "name contains 'Player'"
                        var searchTerm = pattern.Replace("*", "").Trim();
                        if (!string.IsNullOrEmpty(searchTerm))
                        {
                            nameConditions.Add($"name contains '{searchTerm}'");
                        }
                    }
                    else
                    {
                        // 정확한 이름 매칭
                        nameConditions.Add($"name = '{pattern}'");
                    }
                }
            }

            if (nameConditions.Count > 0)
            {
                // OR 조건으로 결합
                string nameQuery = "(" + string.Join(" or ", nameConditions) + ")";
                baseQuery += " and " + nameQuery;
            }
        }

        var request = service.Files.List();
        request.Q = baseQuery;
        request.Fields = "files(id, name)";

        Logger.Instance.Log($"[GAuthService] Query: {baseQuery}");
        Logger.Instance.Log($"[GAuthService] Req FindFiles from {folderLink}");

        var result = request.Execute();
        Logger.Instance.Log($"[GAuthService] Got File List (count: {result.Files.Count})");

        // 결과 로깅
        foreach (var file in result.Files)
        {
            Logger.Instance.Log($"[GAuthService] Found file: {file.Name} (ID: {file.Id})");
        }

        return result.Files.ToList();
    }

    public static IList<Google.Apis.Drive.v3.Data.File> GetAllGoogleSheets(string folderLink)
    {
        var service = GAuthAPIHandler.GetDriveService();

        // 폴더 링크에서 폴더 ID 추출
        string folderId = ExtractFolderId(folderLink);
        Logger.Instance.Log($"[GAuthService] Get Google Sheets from : {folderLink} -> {folderId}");

        // 폴더 내의 Google Sheets 파일만 검색하는 올바른 쿼리
        var request = service.Files.List();
        request.Q = $"'{folderId}' in parents and mimeType='application/vnd.google-apps.spreadsheet' and trashed=false";
        request.Fields = "files(id, name)";
        Logger.Instance.Log($"[GAuthService] Req FindGoogleSheet from {folderLink}");

        var result = request.Execute();
        Console.WriteLine($"[GAuthService] Got Google Sheet List (count: {result.Files.Count})");
        return result.Files;
    }
    
    /// <summary>
    /// Google Drive 링크에서 폴더 ID 추출
    /// </summary>
    private static string ExtractFolderId(string folderIdOrLink)
    {
        // 이미 ID만 있는 경우
        if (!folderIdOrLink.Contains("/"))
            return folderIdOrLink;
            
        // 링크에서 ID 추출
        // 예: https://drive.google.com/drive/folders/FOLDER_ID
        var parts = folderIdOrLink.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "folders" && i + 1 < parts.Length)
            {
                return parts[i + 1].Split('?')[0]; // 쿼리 파라미터 제거
            }
        }
        
        // ID 추출 실패시 원본 반환
        return folderIdOrLink;
    }
}

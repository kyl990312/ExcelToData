using System.Data;
using Common.Utils;
using GAuth.Core;

namespace GAuth;

public static class GAuthService
{
/// <summary>
/// 구글 드라이브 폴더 내의 모든 구글 시트를 읽어 데이터 테이블로 생성합니다.
/// 하나의 시트에 여러 탭이 존재하는 경우 각각 하나의 테이블이 됩니다.
/// 테이블의 이름(시트의 탭)이 #이나 //로 시작하는 경우 테이블 생성에서 제외됩니다.
/// </summary>
/// <param name="folderLink">구글 드라이브 폴더 경로</param>
/// <returns>폴더 내에 존재하는 구글 시트 파일이 갖는 모든 테이블</returns>
public static List<DataTable> GetTableFromGSheet(string folderLink)
{
    List<DataTable> tables = new();
    
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
                    Logger.Instance.Log($"[GAuthService] Converting sheet tab to DataTable: {sheetTitle}");
                    
                    // 시트 데이터 읽기
                    var range = $"{sheetTitle}!A:Z"; // A부터 Z열까지 모든 행
                    var request = sheetsService.Spreadsheets.Values.Get(sheetFile.Id, range);
                    var response = request.Execute();
                    
                    if (response.Values != null && response.Values.Count > 0)
                    {
                        var table = ConvertToDataTable(response.Values, $"{sheetFile.Name}_{sheetTitle}");
                        if (table != null && table.Rows.Count > 0)
                        {
                            tables.Add(table);
                            Logger.Instance.Log($"[GAuthService] Successfully created DataTable: {table.TableName} ({table.Rows.Count} rows, {table.Columns.Count} columns)");
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
                Logger.Instance.Error($"[GAuthService] Error processing sheet {sheetFile.Name}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Instance.Error($"[GAuthService] Error in GetTableFromGSheet: {ex.Message}");
    }
    
    Logger.Instance.Log($"[GAuthService] Total DataTables created: {tables.Count}");
    return tables;
}

/// <summary>
/// Google Sheets의 값들을 DataTable로 변환합니다.
/// 모든 데이터는 Text(string) 타입으로 처리됩니다.
/// </summary>
/// <param name="values">Google Sheets에서 읽어온 값들</param>
/// <param name="tableName">생성할 DataTable의 이름</param>
/// <returns>변환된 DataTable</returns>
private static DataTable ConvertToDataTable(IList<IList<object>> values, string tableName)
{
    var table = new DataTable(tableName);
    
    if (values == null || values.Count == 0)
        return table;
    
    try
    {
        for (int rowIndex = 0; rowIndex < values.Count; rowIndex++)
        {
            var headerRow = values[0];
            for (int i = 0; i < headerRow.Count; i++)
            {
                var columnName = headerRow[i]?.ToString() ?? $"Column{i + 1}";
                table.Columns.Add(columnName, typeof(string));
            }
            var row = values[rowIndex];
            
            var dataRow = table.NewRow();
            int columnIndex = 0;
            
            for (int cellIndex = 0; cellIndex < row.Count && columnIndex < table.Columns.Count; cellIndex++)
            {
                if (columnIndex < table.Columns.Count)
                {
                    // 모든 데이터를 string으로 변환
                    dataRow[columnIndex] = row[cellIndex]?.ToString() ?? string.Empty;
                    columnIndex++;
                }
            }
            
            table.Rows.Add(dataRow);
        }
        
        Logger.Instance.Log($"[GAuthService] DataTable created: {tableName} with {table.Rows.Count} rows and {table.Columns.Count} columns");
    }
    catch (Exception ex)
    {
        Logger.Instance.Error($"[GAuthService] Error converting to DataTable: {ex.Message}");
    }
    
    return table;
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
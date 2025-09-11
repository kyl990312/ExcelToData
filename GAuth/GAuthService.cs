using System.Data;
using Common.Utils;
using GAuth.Core;

namespace GAuth;

public static class GAuthService
{
    public static List<DataTable> GetTableFromGSheet(string folderLink)
    {
        List<DataTable> tables = new();
        var sheets = GetTableFromGSheet(folderLink);
        foreach (var sheet in sheets)
        {
            var table = new DataTable();
            //TODO sheet to table
        }
        return tables;
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
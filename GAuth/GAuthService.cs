using System.Data;
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

    public static IList<Google.Apis.Drive.v3.Data.File> GetAllGoogleSheets(string folderLink)
    {
        var service = GAuthAPIHandler.GetDriveService();

        // mimeType이 spreadsheet인 파일 검색
        var request = service.Files.List();
        request.Q = $"mimeType='{folderLink}'";
        request.Fields = "files(id, name)";
    
        var result = request.Execute();
        Console.WriteLine($"[GAuthService] Got Google Sheet List (count: {result.Files.Count})");
        return result.Files;
    }
}
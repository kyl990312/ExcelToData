using GAuth;

var sheets = GAuth.GAuthService.GetAllGoogleSheets("https://drive.google.com/drive/folders/1zsxI1yQaRRbSOrVba6aQl_ulroYU_YkZ");
foreach (var sheet in sheets)
{
    Console.WriteLine($"sheet: {sheet.Name}");
}
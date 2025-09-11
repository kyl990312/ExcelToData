using GAuth;

string[] searchPatterns = ["*.xls", "*.xlsx"];
var sheets = GAuth.GAuthService.GetAllFiles("https://drive.google.com/drive/folders/1zsxI1yQaRRbSOrVba6aQl_ulroYU_YkZ", searchPatterns);
foreach (var sheet in sheets)
{
    Console.WriteLine($"sheet: {sheet.Name}");
}
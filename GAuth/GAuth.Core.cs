using System.Diagnostics;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;
using System.IO;
using System.Threading;
using Common.Command;
using Newtonsoft.Json;

namespace GAuth.Core;

internal static class GAuthAPIHandler
{
    static string[] Scopes = {
        DriveService.Scope.DriveReadonly,
        SheetsService.Scope.SpreadsheetsReadonly
    };

    private static readonly string CONFIG_PATH = "C:\\PROJECT\\TableAutomarionTool\\Output\\Debug\\net9.0\\Config\\GAuthConfig.json";

    private static UserCredentialConfig _config;
    public static void Initialize()
    {
        if (FileLoader.LoadAndConvertJson(CONFIG_PATH, out _config) == false)
        {
            Console.Error.WriteLine($"[GAuth] Error loading config: {CONFIG_PATH}");
        }
    }
       
    public static DriveService GetDriveService()
    {
        var credential = GetCredential();
        return new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = _config.ApplicationName,
        });
    }

    public static SheetsService GetSheetsService()
    {
        var credential = GetCredential();
        return new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = _config.ApplicationName,
        });
    }

    private static UserCredential GetCredential()
    {
        if (_config == null)
        {
            Initialize();
        }
        
        using var stream = new FileStream(_config.ClientSceret, FileMode.Open, FileAccess.Read);
        string credPath = "token.json";

        return GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            _config.User,
            CancellationToken.None,
            new FileDataStore(credPath, true)).Result;
    }
 
}
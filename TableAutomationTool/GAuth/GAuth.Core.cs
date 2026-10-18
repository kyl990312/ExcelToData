using System.Diagnostics;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;
using System.IO;
using System.Threading;
using TableAutomationTool.Common.Command;
using Newtonsoft.Json;

namespace TableAutomationTool.GAuth.Core;

internal static class GAuthAPIHandler
{
    static string[] Scopes = {
        DriveService.Scope.DriveReadonly,
        SheetsService.Scope.Spreadsheets
    };

    private static readonly string CONFIG_PATH = Path.Combine(
        Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty,
        "Config/GAuthConfig.json"
    );

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

    private const string TokenStorePath = "token.json";

    /// <summary>
    /// token.json 폴더(작업 폴더 기준)에 캐시된 토큰 파일이 있는지 본다.
    /// </summary>
    public static bool HasCachedToken()
        => Directory.Exists(TokenStorePath) && Directory.EnumerateFiles(TokenStorePath).Any();

    /// <summary>
    /// 지정한 설정으로 Google 인증을 수행하고 토큰을 token.json 폴더(작업 폴더 기준)에 캐시한다.
    /// TableEditor 의 [Google 인증] 버튼이 호출한다.
    ///
    /// - 캐시된 토큰이 있으면 브라우저 없이 끝나고, 없으면 브라우저를 열어 로그인을 기다린다.
    /// - 인증 후 Drive 계정 정보를 조회해 실제로 연결되는지 확인하고, 로그인한 계정 이메일을 돌려준다.
    /// - 캐시된 토큰이 만료·폐기된 경우(테스트 게시 상태의 7일 만료 등) 토큰을 지우고 한 번 다시 로그인한다.
    /// - 이후 같은 프로세스의 GSheet 호출도 이 설정을 쓴다.
    /// </summary>
    public static string Authorize(UserCredentialConfig config, CancellationToken cancellationToken)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.ClientSceret))
            throw new InvalidOperationException("client_secret 경로가 비어 있습니다.");
        if (string.IsNullOrWhiteSpace(config.User))
            throw new InvalidOperationException("사용자 값이 비어 있습니다. 토큰 캐시 키로 쓰므로 아무 값이나 넣어야 합니다.");

        var secretPath = Path.GetFullPath(config.ClientSceret);
        if (!File.Exists(secretPath))
            throw new FileNotFoundException(
                $"OAuth 클라이언트 파일이 없습니다: {secretPath} " +
                "(Google Cloud Console 에서 '데스크톱 앱' OAuth 클라이언트 JSON 을 받아 이 경로에 두세요)",
                secretPath);

        _config = config;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return AuthorizeOnce(config, secretPath, cancellationToken);
            }
            catch (Exception ex) when (attempt == 1 && IsTokenError(ex))
            {
                Console.WriteLine($"[GAuth] 캐시된 토큰을 쓸 수 없습니다({ex.Message}). 토큰을 지우고 다시 로그인합니다.");
                if (Directory.Exists(TokenStorePath))
                    Directory.Delete(TokenStorePath, true);
            }
        }
    }

    private static string AuthorizeOnce(UserCredentialConfig config, string secretPath, CancellationToken cancellationToken)
    {
        UserCredential credential;
        using (var stream = new FileStream(secretPath, FileMode.Open, FileAccess.Read))
        {
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                config.User,
                cancellationToken,
                new FileDataStore(TokenStorePath, true)).GetAwaiter().GetResult();
        }

        using var drive = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = config.ApplicationName,
        });
        var request = drive.About.Get();
        request.Fields = "user(emailAddress,displayName)";
        var user = request.Execute().User;
        return user?.EmailAddress ?? user?.DisplayName ?? "(계정 정보 없음)";
    }

    private static bool IsTokenError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is Google.Apis.Auth.OAuth2.Responses.TokenResponseException)
                return true;
        }
        return false;
    }

    private static UserCredential GetCredential()
    {
        if (_config == null)
        {
            Initialize();
        }
        
        try
        {
            using var stream = new FileStream(_config.ClientSceret, FileMode.Open, FileAccess.Read);
            string credPath = "token.json";

            var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                _config.User,
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
                
            return credential;
        }
        catch (AggregateException ex) when (ex.InnerException is Google.Apis.Auth.OAuth2.Responses.TokenResponseException tokenEx)
        {
            Console.WriteLine($"[GAuth] Token error: {tokenEx.Error.Error} - {tokenEx.Error.ErrorDescription}");
            Console.WriteLine("[GAuth] Attempting to refresh credentials...");
            
            // ��ū ���� �����ϰ� ������ �õ�
            string credPath = "token.json";
            if (Directory.Exists(credPath))
            {
                Directory.Delete(credPath, true);
                Console.WriteLine("[GAuth] Deleted old token. Please re-authenticate.");
            }
            
            // ��õ�
            using var stream = new FileStream(_config.ClientSceret, FileMode.Open, FileAccess.Read);
            return GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                _config.User,
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[GAuth] Fatal error getting credentials: {ex.Message}");
            throw;
        }
    }
 
}
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableAutomationTool.AiTools;

/// <summary>
/// "TableAutomationTool.exe ai &lt;tool&gt; ..." 진입점.
/// stdout 에는 JSON 한 줄(봉투)만 쓰고, 엔진 로그는 모두 stderr 로 보낸다.
///   성공: {"ok":true,"tool":"rows","data":{...},"warnings":[...]}
///   실패: {"ok":false,"tool":"rows","error":{"code":"...","message":"...","hint":"..."},"warnings":[...]}
/// 종료 코드: ok 면 0, 아니면 1.
/// </summary>
public static class AiToolRunner
{
    private const int DefaultTimeoutSeconds = 180;
    private const int DefaultRunTimeoutSeconds = 900;

    private sealed class Envelope
    {
        public bool Ok { get; set; }
        public string Tool { get; set; } = "help";
        public object? Data { get; set; }
        public ErrorInfo? Error { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    private sealed record ErrorInfo(string Code, string Message, string? Hint);

    public static int Run(string[] args)
    {
        var callerDirectory = Directory.GetCurrentDirectory();

        // 한글 테이블·파일 이름이 있으므로 UTF-8 로 고정한다. (OutputEncoding 설정은 Out/Error 를 다시 만들므로 먼저 한다)
        try { Console.OutputEncoding = new UTF8Encoding(false); }
        catch { /* 일부 호스트에서는 설정할 수 없다 */ }

        // 끝까지 되돌리지 않는다. 시간 초과 뒤에도 돌고 있는 작업의 로그가 stdout 의 JSON 에 섞이지 않게 하기 위함이다.
        var realOut = Console.Out;
        var capture = new AiConsoleCapture(Console.Error);
        Console.SetOut(capture);
        Console.SetError(capture);

        var envelope = new Envelope
        {
            Tool = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant() ?? "help"
        };
        var pretty = args.Contains("--pretty", StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = new AiArgs(args);
            envelope.Tool = parsed.Tool;

            if (parsed.Tool == "help")
            {
                envelope.Data = Catalog;
                envelope.Ok = true;
            }
            else
            {
                var timeout = parsed.GetInt("timeout", parsed.Tool == "run" ? DefaultRunTimeoutSeconds : DefaultTimeoutSeconds);
                var task = Task.Run(() => Execute(parsed, callerDirectory, capture));
                if (!task.Wait(TimeSpan.FromSeconds(timeout)))
                {
                    if (AiWriteCommand.TryBlockApply())
                        throw new AiToolException("timeout", $"{timeout}초 안에 끝나지 않았습니다.",
                            "GSheet 인증이 브라우저 로그인을 기다리는 중일 수 있습니다. --timeout 으로 늘리거나 사용자에게 확인하세요.");

                    // 시트 쓰기(batchUpdate)를 이미 보냈다. 여기서 결과를 버리면 적용 여부를 알 수 없어
                    // 재시도가 행을 중복 추가하거나 엉뚱한 행을 지우므로, 끝까지 기다려 실제 결과를 돌려준다.
                    task.Wait();
                    envelope.Warnings.Add($"{timeout}초를 넘겼지만 시트 쓰기를 시작한 뒤라 끝까지 기다렸습니다.");
                }

                var command = task.Result;
                envelope.Data = command.Data;
                envelope.Warnings.AddRange(command.Warnings);
                envelope.Ok = command.Succeeded;
                if (!command.Succeeded)
                    envelope.Error = new ErrorInfo(command.FailureCode ?? "run_failed", command.FailureMessage ?? "실패했습니다.", null);
            }
        }
        catch (Exception ex)
        {
            envelope.Ok = false;
            envelope.Error = ToError(ex is AggregateException { InnerException: { } inner } ? inner : ex);
        }

        realOut.WriteLine(JsonSerializer.Serialize(envelope, CreateJsonOptions(pretty)));
        realOut.Flush();
        return envelope.Ok ? 0 : 1;
    }

    private static AiCommand Execute(AiArgs args, string callerDirectory, AiConsoleCapture capture)
    {
        // 엔진은 Config 상대 경로·token.json·Binary/EnumDB.bin 을 작업 폴더 기준으로 푼다.
        // 어디서 호출해도 같은 결과가 나오도록 exe 폴더로 옮긴다.
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;
        Directory.SetCurrentDirectory(baseDirectory);

        var ctx = AiContext.Load(baseDirectory, callerDirectory);
        AiCommand command = args.Tool switch
        {
            "tables" => new AiTablesCommand(ctx, args),
            "schema" => new AiSchemaCommand(ctx, args),
            "rows" => new AiRowsCommand(ctx, args),
            "diff" => new AiDiffCommand(ctx, args),
            "run" => new AiRunCommand(ctx, args, capture),
            "update" => new AiUpdateCommand(ctx, args),
            "insert" => new AiInsertCommand(ctx, args),
            "delete" => new AiDeleteCommand(ctx, args),
            _ => throw new AiToolException("unknown_tool", $"알 수 없는 툴: {args.Tool}", "ai help 로 목록을 확인하세요.")
        };

        args.EnsureKnown(command.Options, command.MaxPositionals);
        command.Execute(out var message);
        command.LogResult(message);
        return command;
    }

    private static ErrorInfo ToError(Exception ex) => ex switch
    {
        AiToolException tool => new ErrorInfo(tool.Code, tool.Message, tool.Hint),
        _ when FindTokenError(ex) is { } token => new ErrorInfo("gsheet_auth_expired",
            $"Google 토큰을 갱신할 수 없습니다: {token.Error?.Error}",
            "토큰이 만료·폐기되었습니다(인증 라이브러리가 캐시를 지웁니다). 사용자에게 TableEditor 설정 탭의 [Google 인증]을 다시 요청하세요."),
        Google.GoogleApiException google => new ErrorInfo("gsheet_api_error", google.Message,
            "폴더 링크·공유 권한을 확인하세요. 토큰이 만료됐으면 사용자에게 [Google 인증]을 다시 요청하세요."),
        _ => new ErrorInfo("internal_error", $"{ex.GetType().Name}: {ex.Message}", "stderr 로그를 확인하세요.")
    };

    private static Google.Apis.Auth.OAuth2.Responses.TokenResponseException? FindTokenError(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is Google.Apis.Auth.OAuth2.Responses.TokenResponseException token)
                return token;
        }
        return null;
    }

    private static JsonSerializerOptions CreateJsonOptions(bool pretty) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = pretty,
    };

    /// <summary>ai help 의 결과. AI 가 사용법을 스스로 확인하는 용도다.</summary>
    private static readonly object Catalog = new
    {
        usage = "TableAutomationTool.exe ai <tool> [args] [--option value] [--timeout sec] [--pretty]",
        output = "stdout 에 JSON 한 줄. {ok, tool, data | error{code,message,hint}, warnings}. 종료 코드 ok=0, 실패=1. 로그는 stderr.",
        sources = "local = ExcelToDataConfig.SrcDirectory 의 xlsx 시트, gsheet = SrcGDriveLink 폴더의 시트 탭, bin = DstDirectory 의 추출 결과. --source 생략 시 설정(IsLoadFromGSheet)을 따른다.",
        writes = "update·insert·delete 는 GSheet 탭만 고친다(--source 없음, 로컬 xlsx·.bin 은 그대로). 먼저 --dry-run 으로 changes 를 확인하고 적용한다. "
                 + "적용하면 batchUpdate 한 번으로 쓰고 다시 읽어 verified 를 준다. 값은 컬럼 타입으로 검사한다(빈 숫자·변환 불가·enum 멤버 아님은 invalid_value). "
                 + ".bin 반영은 ai run extract 가 따로 한다.",
        tools = new object[]
        {
            new
            {
                name = "tables",
                usage = "ai tables [--source local|gsheet|bin] [--filter text]",
                description = "테이블 목록. 원본 소스면 extracted(.bin 존재 여부), bin 이면 rows·columns 를 함께 준다. gsheet 는 탭 이름만 받는다.",
            },
            new
            {
                name = "schema",
                usage = "ai schema <table> [--source local|gsheet|bin]",
                description = "컬럼 이름·타입·배열 여부·enum 멤버(EnumDB)와 행 수. 데이터 검사 결과(타입 변환 실패, 클라이언트 Enum.Parse 가 실패할 enum 값)는 warnings 에 담긴다.",
            },
            new
            {
                name = "rows",
                usage = "ai rows <table> [--source ...] [--where \"Col=Val\"]... [--search text] [--columns A,B] [--offset N] [--limit N]",
                description = "행 조회. --where 는 = != ~(포함) > < >= <= 를 받고 여러 개면 AND. 배열 컬럼은 요소 하나라도 맞으면 매치. "
                              + "--limit 기본 50, 0 이면 전부. 원본 값은 추출기가 .bin 에 쓰는 규칙으로 타입 변환한다. 각 행의 _row 는 0부터 센 데이터 행 번호.",
            },
            new
            {
                name = "diff",
                usage = "ai diff [--path dir]",
                description = "git 기준 변경된 워크북(files·workbooks)과 그 안의 테이블(tables). 로컬 xlsx 만 해당.",
            },
            new
            {
                name = "run",
                usage = "ai run <extract|generate|enum|all> [--table name]...",
                description = "엔진 실행(파일을 쓴다). 단계별 ok·changedFiles·errors·warnings. 실패하면 이후 단계는 skipped. "
                              + "추출 소스는 --source 가 아니라 설정을 따른다. --table 은 테이블(시트) 또는 워크북 이름.",
            },
            new
            {
                name = "update",
                usage = "ai update <table> (--row N... | --where \"Col=Val\"...) --set \"Col=Val\"... [--all] [--dry-run]",
                description = "GSheet 셀 값 변경. --row 는 ai rows 의 _row(반복 가능), --where 는 rows 와 같은 문법(AND). 둘 다 주면 --where 가 가드(맞지 않는 --row 가 있으면 no_match). "
                              + "--where 만으로 2행 이상이면 --all 필요. 이미 같은 값인 셀은 건너뛴다(unchanged). "
                              + "--set 값은 첫 '=' 뒤 전체(문자열은 앞뒤 공백도 그대로). 배열은 \"[a,b]\", enum 은 멤버 이름 또는 정수.",
            },
            new
            {
                name = "insert",
                usage = "ai insert <table> --set \"Col=Val\"... [--copy N] [--at N] [--dry-run]",
                description = "GSheet 에 행을 끼워 넣는다. --at 은 새 행의 _row(기본 끝 = 마지막 데이터 행 아래, EOF·주석 행은 유지). "
                              + "--copy N 은 _row N 의 값을 복사한 뒤 --set 으로 덮어쓴다. --copy 가 없으면 문자열·배열이 아닌 컬럼은 모두 --set 필요(missing_value).",
            },
            new
            {
                name = "delete",
                usage = "ai delete <table> (--row N... | --where \"Col=Val\"...) [--all] [--dry-run]",
                description = "GSheet 에서 행을 지운다. 대상 선택은 update 와 같다. 지운 행의 값은 changes 에 남는다.",
            },
        },
        errorCodes = new[]
        {
            "invalid_args", "unknown_tool", "config_not_found", "source_not_found", "source_disabled", "table_not_found",
            "column_not_found", "gsheet_not_configured", "gsheet_auth_required", "gsheet_auth_expired", "gsheet_api_error",
            "diff_path_not_configured", "diff_path_not_found", "not_a_git_repository", "git_not_found", "git_failed",
            "run_failed", "no_match", "multiple_matches", "invalid_value", "missing_value", "no_columns", "layout_mismatch", "ambiguous_table", "enum_unavailable",
            "gsheet_permission_denied", "verify_failed", "timeout", "internal_error"
        },
    };
}

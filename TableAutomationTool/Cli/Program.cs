using TableAutomationTool.Common.Utils;
using TableAutomationTool.ExcelToData;
using TableAutomationTool.CodeGenerator;
using TableAutomationTool.EnumGenerator;
using TableAutomationTool.Tester;
using TableAutomationTool.Vcs;

namespace TableAutomationTool.Cli;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();

        // diff 는 목록을 stdout 으로 파이프에 넘겨야 하므로 배너를 찍지 않는다.
        if (command != "diff")
            Logger.Instance.Log("[TableAutomationTool] Starting...");

        var targetFileName = ParseTargetFileName(args);

        try
        {
            switch (command)
            {
                case "extract":
                case "etd":
                    return RunExcelToData(targetFileName);

                case "generate":
                case "gen":
                case "cg":
                    return RunCodeGenerator(targetFileName);

                case "enum":
                    return RunEnumGenerator();

                case "all":
                case "pipeline":
                    return RunAll(targetFileName);

                case "init-config":
                    return RunInitConfig();

                case "test":
                    return RunTester();

                case "diff":
                    return RunDiff(args);

                case "help":
                case "-h":
                case "--help":
                case "?":
                    ShowHelp();
                    return 0;

                default:
                    Logger.Instance.Error($"Unknown command: {command}");
                    ShowHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"Error: {ex.Message}");
            Logger.Instance.Error(ex.StackTrace ?? "");
            return 1;
        }
    }

    static string? ParseTargetFileName(string[] args)
    {
        // -each <filename> 또는 --file <filename> 형식 파싱
        for (int i = 1; i < args.Length; i++)
        {
            if ((args[i] == "-each" || args[i] == "--file" || args[i] == "-f") && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    /// <summary>
    /// targetFileName을 Invoker에 전달할 args 배열로 변환
    /// </summary>
    static string[] BuildSubArgs(string? targetFileName)
    {
        if (!string.IsNullOrEmpty(targetFileName))
            return new[] { "-each", targetFileName };
        return Array.Empty<string>();
    }

    static int RunExcelToData(string? targetFileName)
    {
        Logger.Instance.Log("[TableAutomationTool] Running ExcelToData...");

        try
        {
            var invoker = new ETDCommandInvoker(BuildSubArgs(targetFileName));
            invoker.ExecuteAll();
            Logger.Instance.Log("[TableAutomationTool] ExcelToData completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableAutomationTool] ExcelToData failed: {ex.Message}");
            return 1;
        }
    }

    static int RunCodeGenerator(string? targetFileName)
    {
        Logger.Instance.Log("[TableAutomationTool] Running CodeGenerator...");

        try
        {
            var invoker = new CGCommandInvoker(BuildSubArgs(targetFileName));
            invoker.ExecuteAll();
            Logger.Instance.Log("[TableAutomationTool] CodeGenerator completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableAutomationTool] CodeGenerator failed: {ex.Message}");
            return 1;
        }
    }

    static int RunEnumGenerator()
    {
        Logger.Instance.Log("[TableAutomationTool] Running EnumGenerator...");

        try
        {
            var invoker = new EGCommandInvoker(Array.Empty<string>());
            invoker.ExecuteAll();
            Logger.Instance.Log("[TableAutomationTool] EnumGenerator completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableAutomationTool] EnumGenerator failed: {ex.Message}");
            return 1;
        }
    }

    static int RunAll(string? targetFileName)
    {
        Logger.Instance.Log("[TableAutomationTool] Running full pipeline...");

        // 1. EnumGenerator (Enum은 다른 코드에서 사용될 수 있으므로 먼저 실행)
        Logger.Instance.Log("[TableAutomationTool] Step 1/3: EnumGenerator");
        if (RunEnumGenerator() != 0)
        {
            Logger.Instance.Error("[TableAutomationTool] Pipeline failed at EnumGenerator");
            return 1;
        }

        // 2. ExcelToData (Excel → Binary)
        Logger.Instance.Log("[TableAutomationTool] Step 2/3: ExcelToData");
        if (RunExcelToData(targetFileName) != 0)
        {
            Logger.Instance.Error("[TableAutomationTool] Pipeline failed at ExcelToData");
            return 1;
        }

        // 3. CodeGenerator (Binary → C# Classes)
        Logger.Instance.Log("[TableAutomationTool] Step 3/3: CodeGenerator");
        if (RunCodeGenerator(targetFileName) != 0)
        {
            Logger.Instance.Error("[TableAutomationTool] Pipeline failed at CodeGenerator");
            return 1;
        }

        Logger.Instance.Log("[TableAutomationTool] Full pipeline completed successfully!");
        return 0;
    }

    /// <summary>
    /// 제거된 개별 exe 들의 init-config 진입점을 통합한다.
    /// 각 Invoker 의 CreateInitConfigCommands() 를 그대로 재사용한다.
    /// </summary>
    static int RunInitConfig()
    {
        Logger.Instance.Log("[TableAutomationTool] Generating default configs...");

        try
        {
            // ExcelToDataConfig / JsonConfig / GAuthConfig
            new ETDCommandInvoker(new[] { "init-config" }).ExecuteAll();
            // CodeGeneratorConfig
            new CGCommandInvoker(new[] { "init-config" }).ExecuteAll();
            // EnumGeneratorConfig
            new EGCommandInvoker(new[] { "init-config" }).ExecuteAll();
            // GitConfig
            new VcsCommandInvoker(new[] { "init-config" }).ExecuteAll();

            Logger.Instance.Log("[TableAutomationTool] init-config completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableAutomationTool] init-config failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 제거된 Tester.exe 의 EnumDB 공유 메모리 검증을 서브커맨드로 보존한다.
    /// </summary>
    static int RunTester()
    {
        Logger.Instance.Log("[TableAutomationTool] Running EnumDB shared-memory test...");

        try
        {
            new TestCommandInvoker(Array.Empty<string>()).ExecuteAll();
            Logger.Instance.Log("[TableAutomationTool] test completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableAutomationTool] test failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// git 기준 변경된 테이블을 다룬다.
    ///   diff                      : 목록만 출력 (프롬프트 없음, 파이프 가능)
    ///   diff all|extract|generate : 목록 출력 후 Y/N 확인 → 변경분만 다음 단계 실행
    /// 결과 목록은 stdout, 그 외 모든 메시지는 stderr 로 나가므로 파이프로 넘길 수 있다.
    /// </summary>
    static int RunDiff(string[] args)
    {
        string? next = null;
        string? pathOverride = null;

        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i];

            if (a == "--path" || a == "-p")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("[diff][Error] --path 뒤에 폴더 경로가 필요합니다.");
                    return 1;
                }
                pathOverride = args[++i];
                continue;
            }

            if (a.StartsWith("-"))
            {
                Console.Error.WriteLine($"[diff][Error] 알 수 없는 옵션: {a}");
                Console.Error.WriteLine("[diff] 사용법: TableAutomationTool.exe diff [all|extract|generate] [--path <dir>]");
                return 1;
            }

            if (next is null)
            {
                next = a.ToLowerInvariant();
                continue;
            }

            Console.Error.WriteLine($"[diff][Error] 인자가 너무 많습니다: {a}");
            return 1;
        }

        // 다음 단계 이름은 기존 서브커맨드 이름·별칭을 그대로 재사용한다.
        if (next is not null && !IsKnownDiffNextStep(next))
        {
            Console.Error.WriteLine($"[diff][Error] 알 수 없는 diff 하위 명령: {next}. "
                                    + "사용 가능: all, extract(etd), generate(gen, cg)");
            return 1;
        }

        // 한글 파일명이 실재하므로 출력 인코딩을 UTF-8 로 고정한다.
        try { Console.OutputEncoding = new System.Text.UTF8Encoding(false); }
        catch { /* 일부 호스트에서는 설정할 수 없다 */ }

        // Logger / FileLoader 가 stdout 에 찍는 잡음을 stderr 로 돌린다.
        var realOut = Console.Out;
        DiffListCommand command;
        string message;

        Console.SetOut(Console.Error);
        try
        {
            command = new DiffListCommand(pathOverride);
            command.Execute(out message);
        }
        finally
        {
            Console.SetOut(realOut);
        }

        if (command.GSheetOnlySource)
        {
            Console.Error.WriteLine("[diff][경고] 현재 설정은 GSheet 소스입니다(IsLoadFromGSheet=true, IsLoadFromLocal=false). "
                                    + "SrcDirectory 는 로컬 추출에만 쓰이는 값이며, GSheet 의 변경분은 git 으로 알 수 없습니다.");
        }

        switch (command.Status)
        {
            case DiffListStatus.PathNotConfigured:
                Console.Error.WriteLine("[diff][Error] 대상 폴더를 결정할 수 없습니다. --path <폴더> 로 지정하거나 "
                                        + "Config/GitConfig.json 의 RepositoryPath, 또는 Config/ExcelToDataConfig.json 의 "
                                        + "SrcDirectory 를 설정하세요. (init-config 로 기본 파일을 만들 수 있습니다)");
                return 1;

            case DiffListStatus.PathNotFound:
                Console.Error.WriteLine($"[diff][Error] 폴더가 존재하지 않습니다: {command.ResolvedFolder} "
                                        + $"(출처: {command.PathSource}). --path <폴더> 로 직접 지정하세요.");
                return 1;

            case DiffListStatus.NotARepository:
                Console.Error.WriteLine($"[diff][Error] git 저장소가 아닙니다: {command.ResolvedFolder} "
                                        + "— 테이블 원본이 git 으로 관리되는 폴더를 --path 로 지정하세요.");
                if (!string.IsNullOrWhiteSpace(command.ErrorDetail))
                    Console.Error.WriteLine($"[diff][Error] git: {command.ErrorDetail}");
                return 1;

            case DiffListStatus.GitNotFound:
                Console.Error.WriteLine("[diff][Error] git 실행 파일을 찾을 수 없습니다. PATH 에 git 을 추가하거나 "
                                        + "Config/GitConfig.json 의 GitExecutablePath 를 설정하세요.");
                if (!string.IsNullOrWhiteSpace(command.ErrorDetail))
                    Console.Error.WriteLine($"[diff][Error] {command.ErrorDetail}");
                return 1;

            case DiffListStatus.GitFailed:
                Console.Error.WriteLine($"[diff][Error] git 명령이 실패했습니다: {command.ErrorDetail}");
                return 1;
        }

        Console.Error.WriteLine($"[diff] 대상 폴더: {command.ResolvedFolder} (출처: {command.PathSource})");

        // 변경이 0건이면 Y/N 을 묻지 않는다.
        if (command.ChangedTables.Count == 0)
        {
            Console.Error.WriteLine("[diff] 변경된 테이블이 없습니다.");
            return 0;
        }

        Console.Error.WriteLine($"[diff] 변경된 테이블 {command.ChangedTables.Count}건");

        foreach (var name in command.ChangedTables)
            realOut.WriteLine(name);

        realOut.Flush();

        // 다음 단계가 없으면 목록만 출력하고 끝낸다. (프롬프트 없음 = 어떤 환경에서도 안전)
        if (next is null)
            return 0;

        if (!Confirm(command.ChangedTables.Count))
        {
            Console.Error.WriteLine("[diff] 취소했습니다. 아무 작업도 수행하지 않았습니다.");
            return 0;   // 취소는 실패가 아니므로 종료 코드를 오류로 만들지 않는다.
        }

        // 다음 단계의 파이프라인 로그가 목록(stdout)을 오염시키지 않도록 여기서도 stderr 로 돌린다.
        Console.SetOut(Console.Error);
        try
        {
            return RunDiffPipeline(next, command.ChangedTables, command.ChangedFiles);
        }
        finally
        {
            Console.SetOut(realOut);
        }
    }

    /// <summary>
    /// diff 뒤에 올 수 있는 다음 단계 이름 (기존 서브커맨드 이름·별칭 그대로)
    /// </summary>
    static bool IsKnownDiffNextStep(string next)
    {
        return next is "all" or "extract" or "etd" or "generate" or "gen" or "cg";
    }

    /// <summary>
    /// Y/N 확인. 비대화형(stdin 리다이렉트) 환경에서는 대기하지 않고 즉시 취소한다.
    /// </summary>
    static bool Confirm(int count)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("[diff] 비대화형 환경입니다(stdin 이 리다이렉트됨). 확인 없이 진행하지 않고 취소합니다.");
            Console.Error.WriteLine("[diff] 목록만 필요하면 'diff' 를, 자동 실행이 필요하면 목록을 파이프로 받아 "
                                    + "'extract -each <이름>' 을 직접 호출하세요.");
            return false;
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Console.Error.Write($"[diff] 변경된 테이블 {count}건을 처리하시겠습니까? (Y/N): ");

            var line = Console.ReadLine();
            if (line is null)
            {
                // EOF - 이중 안전장치
                Console.Error.WriteLine();
                return false;
            }

            var input = line.Trim();
            if (input.Equals("y", StringComparison.OrdinalIgnoreCase))
                return true;
            if (input.Equals("n", StringComparison.OrdinalIgnoreCase))
                return false;

            Console.Error.WriteLine("[diff] Y 또는 N 을 입력하세요.");
        }

        Console.Error.WriteLine("[diff] 입력을 이해하지 못했습니다. 취소합니다.");
        return false;
    }

    /// <summary>
    /// 변경된 테이블만 기존 파이프라인에 넘긴다.
    /// 새 배치 API 를 만들지 않고 기존 "-each 이름" 경로를 그대로 재사용한다.
    ///   extract : -each 가 "엑셀 파일명" 을 받으므로 tables(파일 베이스명)를 그대로 넘긴다.
    ///   generate: -each 가 "<이름>.bin" 을 찾는데 .bin 이름은 워크시트 이름이므로
    ///             엑셀 파일을 열어 워크시트 이름으로 변환한 뒤 넘긴다.
    /// </summary>
    static int RunDiffPipeline(string next, IReadOnlyList<string> tables, IReadOnlyList<string> changedFiles)
    {
        try
        {
            // enum 은 테이블 단위가 아니므로 전역으로 1회만 실행한다.
            if (next == "all")
            {
                Logger.Instance.Log("[diff] EnumGenerator 실행");
                new EGCommandInvoker(Array.Empty<string>()).ExecuteAll();
            }

            if (next is "all" or "extract" or "etd")
            {
                foreach (var name in tables)
                {
                    Logger.Instance.Log($"[diff] ExcelToData: {name}");
                    new ETDCommandInvoker(new[] { "-each", name }).ExecuteAll();
                }
            }

            if (next is "all" or "generate" or "gen" or "cg")
            {
                var binTargets = ResolveBinaryTargets(changedFiles, tables);
                foreach (var name in binTargets)
                {
                    Logger.Instance.Log($"[diff] CodeGenerator: {name}");
                    new CGCommandInvoker(new[] { "-each", name }).ExecuteAll();
                }
            }

            Logger.Instance.Log("[diff] 완료");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[diff] 파이프라인 실패: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 변경된 엑셀 파일들을 CodeGenerator 가 찾는 이름(= .bin 이름 = 워크시트 이름)으로 변환한다.
    /// 엑셀 1개가 여러 시트를 가지면 .bin 도 시트 수만큼 생기므로 1:N 변환이 된다.
    /// 파일을 열 수 없으면 기존 동작대로 파일 베이스명을 그대로 쓴다.
    /// </summary>
    static IReadOnlyList<string> ResolveBinaryTargets(IReadOnlyList<string> changedFiles,
                                                      IReadOnlyList<string> fallbackTables)
    {
        if (changedFiles.Count == 0)
            return fallbackTables;

        var names = new List<string>();

        foreach (var file in changedFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(file);

            try
            {
                if (!File.Exists(file))
                {
                    names.Add(baseName);
                    continue;
                }

                using var workbook = new ClosedXML.Excel.XLWorkbook(file);
                var sheets = workbook.Worksheets.Select(w => w.Name).ToArray();

                if (sheets.Length == 0)
                {
                    names.Add(baseName);
                    continue;
                }

                names.AddRange(sheets);
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"[diff] 워크시트 목록을 읽지 못했습니다 ({baseName}): {ex.Message}. "
                                        + "파일명을 그대로 사용합니다.");
                names.Add(baseName);
            }
        }

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    static void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  TableAutomationTool - Table Automation Tool CLI");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  tableautomationtool <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  extract, etd          Excel 파일을 Binary로 변환");
        Console.WriteLine("  generate, gen, cg     Binary를 C# 클래스로 생성");
        Console.WriteLine("  enum                  Enum 코드 생성");
        Console.WriteLine("  all, pipeline         전체 파이프라인 실행 (enum → extract → generate)");
        Console.WriteLine("  init-config           기본 Config 파일 생성");
        Console.WriteLine("  test                  EnumDB 공유 메모리 검증");
        Console.WriteLine("  diff                  git 변경 테이블 목록 출력 (프롬프트 없음, 파이프 가능)");
        Console.WriteLine("  diff all              변경분만 전체 파이프라인 (Y/N 확인)");
        Console.WriteLine("  diff extract|etd      변경분만 추출 (Y/N 확인)");
        Console.WriteLine("  diff generate|gen|cg  변경분만 코드 생성 (Y/N 확인)");
        Console.WriteLine("  help, -h, --help      이 도움말 표시");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -each <filename>      특정 파일만 처리 (확장자 제외)");
        Console.WriteLine("  -f, --file <filename> -each와 동일");
        Console.WriteLine();
        Console.WriteLine("Options (diff 전용):");
        Console.WriteLine("  -p, --path <dir>      diff 대상 폴더 오버라이드");
        Console.WriteLine("                        (우선순위: --path > GitConfig.RepositoryPath > ExcelToDataConfig.SrcDirectory)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # 전체 파이프라인 실행");
        Console.WriteLine("  TableAutomationTool.exe all");
        Console.WriteLine();
        Console.WriteLine("  # 특정 테이블만 처리 (Item.xlsm → Item.bin → Item.cs)");
        Console.WriteLine("  TableAutomationTool.exe all -each Item");
        Console.WriteLine();
        Console.WriteLine("  # Excel만 Binary로 변환");
        Console.WriteLine("  TableAutomationTool.exe extract");
        Console.WriteLine();
        Console.WriteLine("  # 특정 Excel 파일만 Binary로 변환");
        Console.WriteLine("  TableAutomationTool.exe extract -each Character");
        Console.WriteLine();
        Console.WriteLine("  # Binary를 C# 클래스로 생성");
        Console.WriteLine("  TableAutomationTool.exe generate");
        Console.WriteLine();
        Console.WriteLine("  # 특정 Binary만 C# 클래스로 생성");
        Console.WriteLine("  TableAutomationTool.exe gen -f Item");
        Console.WriteLine();
        Console.WriteLine("  # Enum 코드만 생성");
        Console.WriteLine("  TableAutomationTool.exe enum");
        Console.WriteLine();
        Console.WriteLine("  # 기본 Config 파일 생성");
        Console.WriteLine("  TableAutomationTool.exe init-config");
        Console.WriteLine();
        Console.WriteLine("  # 변경된 테이블 목록 출력 (목록만, 파이프 가능)");
        Console.WriteLine("  TableAutomationTool.exe diff");
        Console.WriteLine(@"  TableAutomationTool.exe diff --path ""C:\Work\Tables""");
        Console.WriteLine("  TableAutomationTool.exe diff all");
        Console.WriteLine("  TableAutomationTool.exe diff extract");
        Console.WriteLine("  TableAutomationTool.exe diff | ForEach-Object { TableAutomationTool.exe extract -each $_ }");
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }
}

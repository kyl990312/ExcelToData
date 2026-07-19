using TableAutomation.Common.Utils;
using ExcelToData;
using CodeGenerator;
using EnumGenerator;

namespace TableTool;

class Program
{
    static int Main(string[] args)
    {
        Logger.Instance.Log("[TableTool] Starting...");

        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();
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
        Logger.Instance.Log("[TableTool] Running ExcelToData...");

        try
        {
            var invoker = new ETDCommandInvoker(BuildSubArgs(targetFileName));
            invoker.ExecuteAll();
            Logger.Instance.Log("[TableTool] ExcelToData completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableTool] ExcelToData failed: {ex.Message}");
            return 1;
        }
    }

    static int RunCodeGenerator(string? targetFileName)
    {
        Logger.Instance.Log("[TableTool] Running CodeGenerator...");

        try
        {
            var invoker = new CGCommandInvoker(BuildSubArgs(targetFileName));
            invoker.ExecuteAll();
            Logger.Instance.Log("[TableTool] CodeGenerator completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableTool] CodeGenerator failed: {ex.Message}");
            return 1;
        }
    }

    static int RunEnumGenerator()
    {
        Logger.Instance.Log("[TableTool] Running EnumGenerator...");

        try
        {
            var invoker = new EGCommandInvoker(Array.Empty<string>());
            invoker.ExecuteAll();
            Logger.Instance.Log("[TableTool] EnumGenerator completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[TableTool] EnumGenerator failed: {ex.Message}");
            return 1;
        }
    }

    static int RunAll(string? targetFileName)
    {
        Logger.Instance.Log("[TableTool] Running full pipeline...");

        // 1. EnumGenerator (Enum은 다른 코드에서 사용될 수 있으므로 먼저 실행)
        Logger.Instance.Log("[TableTool] Step 1/3: EnumGenerator");
        if (RunEnumGenerator() != 0)
        {
            Logger.Instance.Error("[TableTool] Pipeline failed at EnumGenerator");
            return 1;
        }

        // 2. ExcelToData (Excel → Binary)
        Logger.Instance.Log("[TableTool] Step 2/3: ExcelToData");
        if (RunExcelToData(targetFileName) != 0)
        {
            Logger.Instance.Error("[TableTool] Pipeline failed at ExcelToData");
            return 1;
        }

        // 3. CodeGenerator (Binary → C# Classes)
        Logger.Instance.Log("[TableTool] Step 3/3: CodeGenerator");
        if (RunCodeGenerator(targetFileName) != 0)
        {
            Logger.Instance.Error("[TableTool] Pipeline failed at CodeGenerator");
            return 1;
        }

        Logger.Instance.Log("[TableTool] Full pipeline completed successfully!");
        return 0;
    }

    static void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("  TableTool - Table Automation Tool CLI");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  tabletool <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  extract, etd          Excel 파일을 Binary로 변환");
        Console.WriteLine("  generate, gen, cg     Binary를 C# 클래스로 생성");
        Console.WriteLine("  enum                  Enum 코드 생성");
        Console.WriteLine("  all, pipeline         전체 파이프라인 실행 (enum → extract → generate)");
        Console.WriteLine("  help, -h, --help      이 도움말 표시");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -each <filename>      특정 파일만 처리 (확장자 제외)");
        Console.WriteLine("  -f, --file <filename> -each와 동일");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # 전체 파이프라인 실행");
        Console.WriteLine("  tabletool all");
        Console.WriteLine();
        Console.WriteLine("  # 특정 테이블만 처리 (Item.xlsm → Item.bin → Item.cs)");
        Console.WriteLine("  tabletool all -each Item");
        Console.WriteLine();
        Console.WriteLine("  # Excel만 Binary로 변환");
        Console.WriteLine("  tabletool extract");
        Console.WriteLine();
        Console.WriteLine("  # 특정 Excel 파일만 Binary로 변환");
        Console.WriteLine("  tabletool extract -each Character");
        Console.WriteLine();
        Console.WriteLine("  # Binary를 C# 클래스로 생성");
        Console.WriteLine("  tabletool generate");
        Console.WriteLine();
        Console.WriteLine("  # 특정 Binary만 C# 클래스로 생성");
        Console.WriteLine("  tabletool gen -f Item");
        Console.WriteLine();
        Console.WriteLine("  # Enum 코드만 생성");
        Console.WriteLine("  tabletool enum");
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }
}

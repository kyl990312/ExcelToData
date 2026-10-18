using System.Globalization;
using TableAutomationTool.CodeGenerator;
using TableAutomationTool.Common.Command;
using TableAutomationTool.EnumGenerator;
using TableAutomationTool.ExcelParser;
using TableAutomationTool.ExcelParser.Core;
using TableAutomationTool.ExcelToData;
using TableAutomationTool.Shared;
using TableAutomationTool.UserEnum;
using TableAutomationTool.Vcs;

namespace TableAutomationTool.AiTools;

/// <summary>
/// AI 툴 명령의 공통 부분. Execute 는 Data 를 채우고, 실패는 AiToolException 으로 알린다.
/// </summary>
public abstract class AiCommand : Command
{
    protected readonly AiContext Ctx;
    protected readonly AiArgs Args;

    protected AiCommand(AiContext ctx, AiArgs args)
    {
        Ctx = ctx;
        Args = args;
    }

    /// <summary>이 명령이 받는 옵션 (공통 옵션 제외)</summary>
    public abstract IReadOnlyCollection<string> Options { get; }

    /// <summary>받는 위치 인자 수</summary>
    public virtual int MaxPositionals => 0;

    public object? Data { get; protected set; }
    public List<string> Warnings { get; } = new();

    /// <summary>false 면 Data 는 있지만 결과가 실패다 (run 의 단계 실패)</summary>
    public bool Succeeded { get; protected set; } = true;

    public string? FailureMessage { get; protected set; }

    /// <summary>Succeeded 가 false 일 때의 오류 코드. 없으면 run_failed.</summary>
    public string? FailureCode { get; protected set; }

    protected AiTableSourceKind ResolveSource()
    {
        var kind = Ctx.ResolveSource(Args.Get("source"));
        if (!Args.Has("source") && Ctx.BothSourcesEnabled)
            Warnings.Add("설정에 로컬·GSheet 소스가 모두 켜져 있습니다. 추출은 둘 다 돌고 GSheet 결과가 남으므로 gsheet 를 기본으로 씁니다.");
        return kind;
    }

    private const int MaxEnumWarnings = 20;

    /// <summary>
    /// 테이블을 읽고, 경고(타입 변환 실패·enum 값 오류)를 Warnings 에 모은다.
    /// </summary>
    protected AiTable LoadTable(string name, AiTableSourceKind kind)
    {
        var table = AiTableSource.Load(Ctx, kind, name);
        Warnings.AddRange(table.Warnings);
        CheckEnumValues(table);
        return table;
    }

    /// <summary>
    /// 클라이언트는 enum 셀을 Enum.Parse(type, value, ignoreCase: true) 로 읽는다(InfoTable).
    /// 그 호출이 실패할 값 — EnumDB 멤버도 정수도 아닌 값, 빈 값 — 을 경고로 알린다.
    /// </summary>
    private void CheckEnumValues(AiTable table)
    {
        var enumColumns = table.Columns.Where(c => c.DataType == EDataType.UserDefinedEnum).ToList();
        if (enumColumns.Count == 0)
            return;

        new LoadEnumDBCommand().Execute(out var enumResult);
        if (EnumDB.Instance.IsEmpty)
        {
            Warnings.Add($"EnumDB 를 불러오지 못해 enum 값을 검사하지 못했습니다({enumResult}). 'ai run enum' 으로 만들 수 있습니다.");
            return;
        }

        var failures = 0;
        foreach (var column in enumColumns.Where(c => EnumDB.Instance.HasEnum(c.EnumType)))
        {
            var members = EnumDB.Instance.GetEnumMembers(column.EnumType)
                .Select(m => m.member)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                table.Rows[i].TryGetValue(column.Name, out var value);
                var values = value is Array array ? array.Cast<object?>() : new[] { value };

                foreach (var text in values.Select(v => v?.ToString() ?? string.Empty))
                {
                    if (members.Contains(text) || int.TryParse(text, out _))
                        continue;

                    if (++failures <= MaxEnumWarnings)
                        Warnings.Add($"행 {i} '{column.Name}': '{text}' 는 {column.EnumType} 멤버가 아닙니다 — 클라이언트 Enum.Parse 가 실패합니다. (멤버: {string.Join(", ", members)})");
                }
            }
        }

        if (failures > MaxEnumWarnings)
            Warnings.Add($"enum 값 오류가 {failures - MaxEnumWarnings}건 더 있습니다.");
    }
}

/// <summary>
/// ai tables — 원본(로컬 xlsx 시트 / GSheet 탭) 또는 .bin 의 테이블 목록
/// </summary>
public sealed class AiTablesCommand : AiCommand
{
    public AiTablesCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "source", "filter" };

    public override void Execute(out string result)
    {
        var kind = ResolveSource();
        var filter = Args.Get("filter");

        var entries = AiTableSource.List(Ctx, kind, Warnings);
        if (!string.IsNullOrWhiteSpace(filter))
            entries = entries.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var binNames = kind == AiTableSourceKind.Bin ? null : AiTableSource.GetBinNames(Ctx);

        Data = new
        {
            source = AiContext.SourceName(kind),
            location = AiTableSource.Location(Ctx, kind),
            count = entries.Count,
            tables = entries
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => new
                {
                    name = e.Name,
                    origin = e.Origin,
                    extracted = binNames?.Contains(e.Name),
                    rows = e.Rows,
                    columns = e.Columns,
                }),
        };
        result = $"[AiTablesCommand] {entries.Count} table(s) from {AiContext.SourceName(kind)}";
    }
}

/// <summary>
/// ai schema &lt;table&gt; — 컬럼 이름·타입·배열 여부·enum 멤버
/// </summary>
public sealed class AiSchemaCommand : AiCommand
{
    public AiSchemaCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "source" };
    public override int MaxPositionals => 1;

    public override void Execute(out string result)
    {
        var name = Args.RequirePositional(0, "테이블 이름");
        var kind = ResolveSource();
        var table = LoadTable(name, kind);

        Data = new
        {
            table = table.Name,
            source = AiContext.SourceName(kind),
            origin = table.Origin,
            rowCount = table.Rows.Count,
            columns = table.Columns.Select(DescribeColumn).ToList(),
        };
        result = $"[AiSchemaCommand] {table.Name}: {table.Columns.Count} column(s)";
    }

    private object DescribeColumn(AiColumn column)
    {
        object? enumInfo = null;
        if (column.DataType == EDataType.UserDefinedEnum)
        {
            if (EnumDB.Instance.HasEnum(column.EnumType))
            {
                enumInfo = new
                {
                    name = column.EnumType,
                    members = EnumDB.Instance.GetEnumMembers(column.EnumType)
                        .Select(m => new { name = m.member, value = m.value }),
                };
            }
            else
            {
                enumInfo = new { name = column.EnumType, known = false };
                if (!EnumDB.Instance.IsEmpty)
                    Warnings.Add($"'{column.Name}' 의 타입 '{column.Type}' 은 기본 타입도 아니고 EnumDB 에도 없습니다. 타입 오타인지 확인하세요.");
            }
        }

        return new { name = column.Name, type = column.Type, isArray = column.IsArray, @enum = enumInfo };
    }
}

/// <summary>
/// ai rows &lt;table&gt; — 행 조회·검색
/// </summary>
public sealed class AiRowsCommand : AiCommand
{
    private const int DefaultLimit = 50;

    public AiRowsCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "source", "where", "search", "columns", "offset", "limit" };
    public override int MaxPositionals => 1;

    public override void Execute(out string result)
    {
        var name = Args.RequirePositional(0, "테이블 이름");
        var offset = Args.GetInt("offset", 0);
        var limit = Args.GetInt("limit", DefaultLimit);
        var search = Args.Get("search");
        var conditions = Args.GetAll("where").Select(AiRowFilter.ParseCondition).ToList();

        var kind = ResolveSource();
        var table = LoadTable(name, kind);

        var columnNames = table.Columns.Select(c => c.Name).ToList();
        foreach (var condition in conditions)
            AiRowFilter.RequireColumn(columnNames, condition.Column);

        var projection = ParseColumns(Args.Get("columns"), columnNames);

        var matched = table.Rows
            .Select((row, index) => (row, index))
            .Where(x => conditions.All(c => AiRowFilter.Matches(x.row, c)))
            .Where(x => string.IsNullOrEmpty(search)
                        || x.row.Values.Any(v => AiRowFilter.Format(v).Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var page = matched.Skip(offset);
        if (limit > 0)
            page = page.Take(limit);

        var rows = page.Select(x =>
        {
            var row = new Dictionary<string, object?> { ["_row"] = x.index };
            foreach (var column in projection)
                row[column] = x.row.TryGetValue(column, out var v) ? v : null;
            return row;
        }).ToList();

        Data = new
        {
            table = table.Name,
            source = AiContext.SourceName(kind),
            origin = table.Origin,
            columns = projection,
            total = table.Rows.Count,
            matched = matched.Count,
            offset,
            returned = rows.Count,
            truncated = offset + rows.Count < matched.Count,
            rows,
        };
        result = $"[AiRowsCommand] {table.Name}: {rows.Count}/{matched.Count} row(s)";
    }

    private static List<string> ParseColumns(string? option, List<string> columnNames)
    {
        if (string.IsNullOrWhiteSpace(option))
            return columnNames;

        var wanted = option.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return wanted.Select(w => AiRowFilter.RequireColumn(columnNames, w)).ToList();
    }
}

/// <summary>
/// --where 조건 해석·매칭과 값 문자열화. rows 와 쓰기 툴(update·delete)이 같은 규칙으로 행을 고르도록 공유한다.
/// </summary>
internal static class AiRowFilter
{
    /// <summary>앞에서부터 검사하므로 두 글자 연산자를 먼저 둔다</summary>
    private static readonly string[] Operators = { "!=", ">=", "<=", "=", "~", ">", "<" };

    public sealed record Condition(string Column, string Op, string Value, double? Number);

    public static Condition ParseCondition(string expression)
    {
        for (int i = 0; i < expression.Length; i++)
        {
            foreach (var op in Operators)
            {
                if (string.CompareOrdinal(expression, i, op, 0, op.Length) != 0)
                    continue;

                var column = expression[..i].Trim();
                var value = expression[(i + op.Length)..].Trim();
                if (column.Length == 0)
                    break;

                double? number = null;
                if (op is ">" or "<" or ">=" or "<=")
                {
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                        throw new AiToolException("invalid_args", $"비교 연산자 {op} 에는 숫자가 필요합니다: {expression}");
                    number = n;
                }
                return new Condition(column, op, value, number);
            }
        }

        throw new AiToolException("invalid_args", $"조건 형식이 잘못되었습니다: {expression}",
            "--where \"컬럼=값\" | \"컬럼!=값\" | \"컬럼~부분문자열\" | \"컬럼>=숫자\" (> < >= <=)");
    }

    public static string RequireColumn(List<string> columnNames, string column)
    {
        var found = columnNames.FirstOrDefault(c => c.Equals(column, StringComparison.OrdinalIgnoreCase));
        if (found == null)
            throw new AiToolException("column_not_found", $"컬럼이 없습니다: {column}",
                $"있는 컬럼: {string.Join(", ", columnNames)}");
        return found;
    }

    public static bool Matches(Dictionary<string, object?> row, Condition condition)
    {
        var key = row.Keys.First(k => k.Equals(condition.Column, StringComparison.OrdinalIgnoreCase));
        var value = row[key];

        // 배열 컬럼은 요소 중 하나라도 맞으면 맞는 것으로 본다. (!= 는 어떤 요소도 같지 않을 때)
        var candidates = value is Array array ? array.Cast<object?>().ToList() : new List<object?> { value };

        return condition.Op switch
        {
            "=" => candidates.Any(c => Format(c).Equals(condition.Value, StringComparison.OrdinalIgnoreCase)),
            "!=" => !candidates.Any(c => Format(c).Equals(condition.Value, StringComparison.OrdinalIgnoreCase)),
            "~" => candidates.Any(c => Format(c).Contains(condition.Value, StringComparison.OrdinalIgnoreCase)),
            _ => candidates.Any(c => CompareNumber(c, condition))
        };
    }

    private static bool CompareNumber(object? value, Condition condition)
    {
        if (!double.TryParse(Format(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return false;

        var target = condition.Number!.Value;
        return condition.Op switch
        {
            ">" => n > target,
            "<" => n < target,
            ">=" => n >= target,
            _ => n <= target
        };
    }

    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        Array array => string.Join(",", array.Cast<object?>().Select(Format)),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}

/// <summary>
/// ai diff — git 기준 변경된 워크북과 그 안의 테이블(시트)
/// </summary>
public sealed class AiDiffCommand : AiCommand
{
    public AiDiffCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "path" };

    public override void Execute(out string result)
    {
        var path = Args.Get("path");
        var command = new DiffListCommand(string.IsNullOrWhiteSpace(path) ? null : Ctx.ResolveCallerPath(path));
        command.Execute(out result);

        switch (command.Status)
        {
            case DiffListStatus.PathNotConfigured:
                throw new AiToolException("diff_path_not_configured", "diff 대상 폴더를 정할 수 없습니다.",
                    "--path 로 지정하거나 GitConfig.RepositoryPath / ExcelToDataConfig.SrcDirectory 를 설정하세요.");
            case DiffListStatus.PathNotFound:
                throw new AiToolException("diff_path_not_found", $"폴더가 없습니다: {command.ResolvedFolder} (출처: {command.PathSource})");
            case DiffListStatus.NotARepository:
                throw new AiToolException("not_a_git_repository", $"git 저장소가 아닙니다: {command.ResolvedFolder}", command.ErrorDetail);
            case DiffListStatus.GitNotFound:
                throw new AiToolException("git_not_found", "git 실행 파일을 찾을 수 없습니다.", "GitConfig.GitExecutablePath 를 설정하세요.");
            case DiffListStatus.GitFailed:
                throw new AiToolException("git_failed", $"git 명령이 실패했습니다: {command.ErrorDetail}");
        }

        if (command.GSheetOnlySource)
            Warnings.Add("현재 설정은 GSheet 소스입니다. git diff 는 로컬 xlsx 변경만 알 수 있습니다.");

        var tables = new List<string>();
        foreach (var file in command.ChangedFiles)
        {
            if (File.Exists(file))
                tables.AddRange(TableHelper.GetTableNames(file));
            else
                Warnings.Add($"삭제된 워크북: {Path.GetFileName(file)} — 시트 이름을 알 수 없어 tables 에서 뺐습니다.");
        }

        Data = new
        {
            folder = command.ResolvedFolder,
            pathSource = command.PathSource,
            files = command.ChangedFiles,
            workbooks = command.ChangedTables,
            tables = tables.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase),
        };
    }
}

/// <summary>
/// ai run &lt;extract|generate|enum|all&gt; — 엔진 실행. 단계마다 로그의 오류·경고와 바뀐 파일을 돌려준다.
/// 소스(로컬/GSheet)는 --source 가 아니라 ExcelToDataConfig 설정을 따른다.
/// </summary>
public sealed class AiRunCommand : AiCommand
{
    private const int MaxLines = 30;

    private static readonly string[] ErrorMarkers =
    {
        "[Error]", "] Error:", "Wrong Src Directory", "Wrong Dst Directory", "Binary directory not found",
        "Not found as workbook or table", "Load Enum DB Failed", "Fatal error"
    };

    private static readonly string[] WarningMarkers = { "[Warning]", "Warning:" };

    private sealed record Step(string Name, string? Table)
    {
        public string Label => Table == null ? Name : $"{Name}({Table})";
    }

    private readonly AiConsoleCapture _capture;

    public AiRunCommand(AiContext ctx, AiArgs args, AiConsoleCapture capture) : base(ctx, args)
    {
        _capture = capture;
    }

    public override IReadOnlyCollection<string> Options => new[] { "table" };
    public override int MaxPositionals => 1;

    public override void Execute(out string result)
    {
        var target = Args.RequirePositional(0, "실행 단계(extract | generate | enum | all)").ToLowerInvariant();
        var tables = Args.GetAll("table");
        var steps = BuildSteps(target, tables);

        if (steps.Any(s => s.Name == "extract") && Ctx.Extract.IsLoadFromGSheet)
            Ctx.EnsureGSheetReady();
        if (steps.Any(s => s.Name == "extract") && !Ctx.Extract.IsLoadFromGSheet && !Ctx.Extract.IsLoadFromLocal)
            throw new AiToolException("source_disabled", "ExcelToDataConfig 에서 IsLoadFromLocal·IsLoadFromGSheet 가 모두 꺼져 있어 추출할 것이 없습니다.");

        var results = new List<object>();
        var skipped = new List<string>();

        foreach (var step in steps)
        {
            if (!Succeeded)
            {
                skipped.Add(step.Label);
                continue;
            }

            var stepResult = RunStep(step, out var ok);
            results.Add(stepResult);
            if (!ok)
            {
                Succeeded = false;
                FailureMessage = $"단계 {step.Label} 이(가) 실패했습니다. steps 의 errors 를 확인하세요.";
            }
        }

        Data = new { target, tables, steps = results, skipped };
        result = $"[AiRunCommand] {target}: {(Succeeded ? "ok" : "failed")}";
    }

    private static List<Step> BuildSteps(string target, IReadOnlyList<string> tables)
    {
        List<Step> PerTable(string name) => tables.Count == 0
            ? new List<Step> { new(name, null) }
            : tables.Select(t => new Step(name, t)).ToList();

        return target switch
        {
            "enum" => new List<Step> { new("enum", null) },
            "extract" or "etd" => PerTable("extract"),
            "generate" or "gen" or "cg" => PerTable("generate"),
            "all" or "pipeline" => new List<Step> { new("enum", null) }
                .Concat(PerTable("extract"))
                .Concat(PerTable("generate"))
                .ToList(),
            _ => throw new AiToolException("invalid_args", $"알 수 없는 실행 단계: {target}", "extract | generate | enum | all")
        };
    }

    private object RunStep(Step step, out bool ok)
    {
        var before = Snapshot();
        var mark = _capture.LineCount;
        string? exception = null;

        try
        {
            var args = step.Table == null ? Array.Empty<string>() : new[] { "-each", step.Table };
            CommandInvoker invoker = step.Name switch
            {
                "enum" => new EGCommandInvoker(Array.Empty<string>()),
                "extract" => new ETDCommandInvoker(args),
                _ => new CGCommandInvoker(args)
            };
            invoker.ExecuteAll();
        }
        catch (Exception ex)
        {
            exception = ex.Message;
        }

        var changed = Compare(before, Snapshot());
        var lines = _capture.LinesFrom(mark);
        var errors = lines.Where(l => ErrorMarkers.Any(m => l.Contains(m, StringComparison.Ordinal))).ToList();
        var warnings = lines.Where(l => WarningMarkers.Any(m => l.Contains(m, StringComparison.Ordinal))).ToList();
        if (exception != null)
            errors.Add($"[Exception] {exception}");

        var wroteExpected = WroteExpectedOutput(step, changed);
        if (errors.Count == 0 && !wroteExpected)
            errors.Add(step.Table == null
                ? "출력 파일이 바뀌지 않았습니다."
                : $"'{step.Table}' 의 출력 파일이 바뀌지 않았습니다. 이름이 맞는지 'ai tables' 로 확인하세요.");

        ok = errors.Count == 0;
        return new
        {
            step = step.Name,
            table = step.Table,
            ok,
            changedFiles = changed,
            errors = errors.Take(MaxLines),
            errorCount = errors.Count,
            warnings = warnings.Take(MaxLines),
            warningCount = warnings.Count,
        };
    }

    private static bool WroteExpectedOutput(Step step, List<ChangedFile> changed)
    {
        return step.Name switch
        {
            "enum" => changed.Any(c => Path.GetFileName(c.Path).Equals("EnumDB.bin", StringComparison.OrdinalIgnoreCase)),
            "extract" => changed.Any(c => c.Path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)),
            _ => step.Table == null
                ? changed.Any(c => c.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                : changed.Any(c => Path.GetFileName(c.Path).StartsWith(step.Table + "Info", StringComparison.OrdinalIgnoreCase))
        };
    }

    // ───────────── 산출물 추적 ─────────────

    public sealed record ChangedFile(string Path, string Status);

    private Dictionary<string, (DateTime time, long length)> Snapshot()
    {
        var roots = new List<(string dir, string pattern, SearchOption option)>
        {
            (Ctx.BinDirectory, "*.bin", SearchOption.TopDirectoryOnly),
        };

        var enumPath = Ctx.ResolveConfigPath(SystemPath.EnumBinaryPath);
        roots.Add((Path.GetDirectoryName(enumPath) ?? string.Empty, Path.GetFileName(enumPath), SearchOption.TopDirectoryOnly));

        // ExtractJsonCommand 는 IsLoadFromLocal 을 보지 않고 .bin → json 을 만든다.
        if (Ctx.Json != null)
            roots.Add((Ctx.ResolveConfigPath(Ctx.Json.DstDirectory), "*.json", SearchOption.TopDirectoryOnly));

        if (Ctx.CodeGen != null)
        {
            roots.Add((Ctx.ResolveConfigPath(Ctx.CodeGen.ClassOutputDirectory), "*" + Ctx.CodeGen.CodeExtension, SearchOption.AllDirectories));
            roots.Add((Ctx.ResolveConfigPath(Ctx.CodeGen.ManagerOutputDirectory), "*" + Ctx.CodeGen.CodeExtension, SearchOption.AllDirectories));
        }

        var snapshot = new Dictionary<string, (DateTime, long)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dir, pattern, option) in roots)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                continue;

            foreach (var file in Directory.GetFiles(dir, pattern, option))
            {
                var info = new FileInfo(file);
                snapshot[info.FullName] = (info.LastWriteTimeUtc, info.Length);
            }
        }
        return snapshot;
    }

    private static List<ChangedFile> Compare(Dictionary<string, (DateTime time, long length)> before,
                                             Dictionary<string, (DateTime time, long length)> after)
    {
        var changed = new List<ChangedFile>();

        foreach (var (path, state) in after)
        {
            if (!before.TryGetValue(path, out var old))
                changed.Add(new ChangedFile(path, "created"));
            else if (old != state)
                changed.Add(new ChangedFile(path, "modified"));
        }

        changed.AddRange(before.Keys.Where(p => !after.ContainsKey(p)).Select(p => new ChangedFile(p, "deleted")));
        return changed.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

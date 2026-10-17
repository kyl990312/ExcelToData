using System.Collections;
using ClosedXML.Excel;
using TableAutomationTool.ExcelParser;
using TableAutomationTool.ExcelParser.Core;
using TableAutomationTool.ExcelToData;
using TableAutomationTool.GAuth;

namespace TableAutomationTool.AiTools;

public sealed record AiTableEntry(string Name, string Origin, int? Rows = null, int? Columns = null);

public sealed class AiColumn
{
    public string Name { get; init; } = string.Empty;

    /// <summary>System. 접두어를 뗀 타입 문자열. .bin 헤더에 기록되는 값과 같다 (예: Int32, String[], ECharacterType)</summary>
    public string Type { get; init; } = string.Empty;

    public EDataType DataType { get; init; }
    public bool IsArray { get; init; }

    /// <summary>DataType 이 UserDefinedEnum 일 때의 enum 타입 이름</summary>
    public string EnumType { get; init; } = string.Empty;

    public static AiColumn From(string name, string typeString)
    {
        var (dataType, customTypeName, isArray) = DataTypeHelper.ParseDataType(typeString);
        return new AiColumn
        {
            Name = name,
            Type = typeString.StartsWith("System.") ? typeString[7..] : typeString,
            DataType = dataType,
            IsArray = isArray,
            EnumType = customTypeName,
        };
    }
}

public sealed class AiTable
{
    public string Name { get; init; } = string.Empty;
    public AiTableSourceKind Source { get; init; }

    /// <summary>테이블이 들어 있는 워크북 파일 / 스프레드시트/탭 / .bin 파일</summary>
    public string Origin { get; init; } = string.Empty;

    public List<AiColumn> Columns { get; init; } = new();
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// 테이블 목록·내용을 원본(로컬 xlsx / GSheet) 또는 추출 결과(.bin)에서 읽는다.
/// 원본 값은 추출기(BinaryWriter)가 .bin 에 쓰는 규칙대로 컬럼 타입에 맞춰 바꿔서 돌려준다.
/// </summary>
public static class AiTableSource
{
    private const int MaxConversionWarnings = 20;

    public static string Location(AiContext ctx, AiTableSourceKind kind) => kind switch
    {
        AiTableSourceKind.Local => ctx.SrcDirectory,
        AiTableSourceKind.GSheet => ctx.Extract.SrcGDriveLink,
        _ => ctx.BinDirectory
    };

    public static List<AiTableEntry> List(AiContext ctx, AiTableSourceKind kind, List<string> warnings)
    {
        return kind switch
        {
            AiTableSourceKind.Local => ListLocal(ctx, warnings),
            AiTableSourceKind.GSheet => ListGSheet(ctx),
            _ => ListBin(ctx)
        };
    }

    public static AiTable Load(AiContext ctx, AiTableSourceKind kind, string tableName)
    {
        return kind switch
        {
            AiTableSourceKind.Local => LoadLocal(ctx, tableName),
            AiTableSourceKind.GSheet => LoadGSheet(ctx, tableName),
            _ => LoadBin(ctx, tableName)
        };
    }

    /// <summary>.bin 폴더의 테이블 이름 집합. 폴더가 없으면 빈 집합.</summary>
    public static HashSet<string> GetBinNames(AiContext ctx)
    {
        var dir = ctx.BinDirectory;
        if (!Directory.Exists(dir))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return GetBinFiles(ctx)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // ───────────── 로컬 xlsx ─────────────

    private static string RequireSrcDirectory(AiContext ctx)
    {
        var src = ctx.SrcDirectory;
        if (string.IsNullOrEmpty(src) || !Directory.Exists(src))
            throw new AiToolException("source_not_found", $"원본 폴더가 없습니다: {src}",
                "ExcelToDataConfig.json 의 SrcDirectory 를 확인하세요.");
        return src;
    }

    /// <summary>엔진(ExtractBinaryCommand)과 같은 패턴으로 워크북을 찾는다. Excel 락파일(~$)은 뺀다.</summary>
    private static List<string> GetWorkbooks(string src)
    {
        return ProjectDefine.SearchPatterns
            .SelectMany(p => Directory.GetFiles(src, p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<AiTableEntry> ListLocal(AiContext ctx, List<string> warnings)
    {
        var entries = new List<AiTableEntry>();

        foreach (var workbook in GetWorkbooks(RequireSrcDirectory(ctx)))
        {
            var names = TableHelper.GetTableNames(workbook);
            if (names.Count == 0)
                warnings.Add($"워크북을 열 수 없거나 시트가 없습니다: {Path.GetFileName(workbook)} (Excel 에서 열려 있으면 닫으세요)");

            entries.AddRange(names.Select(n => new AiTableEntry(n, Path.GetFileName(workbook))));
        }

        foreach (var dup in entries.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            warnings.Add($"테이블 이름 중복: {dup.Key} ({string.Join(", ", dup.Select(e => e.Origin))}) — 같은 .bin 을 덮어씁니다.");

        return entries;
    }

    private static AiTable LoadLocal(AiContext ctx, string tableName)
    {
        var seen = new List<string>();
        var skipped = new List<string>();

        foreach (var path in GetWorkbooks(RequireSrcDirectory(ctx)))
        {
            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(path);
            }
            catch (Exception ex)
            {
                skipped.Add($"{Path.GetFileName(path)} ({ex.Message})");
                continue;
            }

            using (workbook)
            {
                var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));
                if (sheet == null)
                {
                    seen.AddRange(workbook.Worksheets.Select(s => s.Name));
                    continue;
                }

                var table = FromCustomTable(TableHelper.GetTable(sheet), AiTableSourceKind.Local, Path.GetFileName(path));
                table.Warnings.AddRange(skipped.Select(s => $"열지 못한 워크북: {s}"));
                return table;
            }
        }

        throw NotFound(tableName, "local", seen, skipped.Count > 0 ? $"열지 못한 워크북: {string.Join(", ", skipped)}" : null);
    }

    // ───────────── GSheet ─────────────

    private static List<AiTableEntry> ListGSheet(AiContext ctx)
    {
        ctx.EnsureGSheetReady();
        return GAuthService.GetTabsFromGSheet(ctx.Extract.SrcGDriveLink)
            .Select(t => new AiTableEntry(t.TableName, $"{t.SpreadsheetName}/{t.SheetTitle}"))
            .ToList();
    }

    private static AiTable LoadGSheet(AiContext ctx, string tableName)
    {
        var matches = FindGSheetTabs(ctx, tableName);
        var tab = matches[0];
        var gsheetTable = GAuthService.GetTableFromGSheetTab(tab);
        var table = FromCustomTable(TableHelper.GetTable(tab.TableName, gsheetTable.Data),
            AiTableSourceKind.GSheet, GSheetOrigin(tab));

        if (matches.Count > 1)
            table.Warnings.Add($"같은 이름의 탭이 여러 개입니다: {string.Join(", ", matches.Select(GSheetOrigin))} — 첫 번째를 읽었습니다.");
        return table;
    }

    /// <summary>
    /// 테이블 이름(ToTableName 규칙)이 같은 GSheet 탭을 모두 찾는다(폴더 순서). 하나도 없으면 table_not_found.
    /// </summary>
    internal static List<GSheetTab> FindGSheetTabs(AiContext ctx, string tableName)
    {
        ctx.EnsureGSheetReady();
        var tabs = GAuthService.GetTabsFromGSheet(ctx.Extract.SrcGDriveLink);
        var matches = tabs.Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            throw NotFound(tableName, "gsheet", tabs.Select(t => t.TableName), null);
        return matches;
    }

    internal static string GSheetOrigin(GSheetTab tab) => $"{tab.SpreadsheetName}/{tab.SheetTitle}";

    // ───────────── .bin ─────────────

    private static IEnumerable<string> GetBinFiles(AiContext ctx)
    {
        return Directory.GetFiles(ctx.BinDirectory, "*.bin")
            .Where(f => ctx.CodeGen?.IsEnumBinary(f) != true)
            .Where(f => !Path.GetFileName(f).Equals("EnumDB.bin", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    private static string RequireBinDirectory(AiContext ctx)
    {
        var dir = ctx.BinDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            throw new AiToolException("source_not_found", $".bin 폴더가 없습니다: {dir}",
                "ExcelToDataConfig.json 의 DstDirectory 를 확인하거나 먼저 추출하세요(ai run extract).");
        return dir;
    }

    private static List<AiTableEntry> ListBin(AiContext ctx)
    {
        RequireBinDirectory(ctx);
        return GetBinFiles(ctx)
            .Select(f =>
            {
                var schema = BinaryParser.BinarySchema.ReadFromBytes(File.ReadAllBytes(f));
                return new AiTableEntry(Path.GetFileNameWithoutExtension(f), Path.GetFileName(f), schema.RowCount, schema.ColumnCount);
            })
            .ToList();
    }

    private static AiTable LoadBin(AiContext ctx, string tableName)
    {
        RequireBinDirectory(ctx);
        var path = GetBinFiles(ctx).FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Equals(tableName, StringComparison.OrdinalIgnoreCase));
        if (path == null)
            throw NotFound(tableName, "bin", GetBinNames(ctx), null);

        var bytes = File.ReadAllBytes(path);
        var schema = BinaryParser.BinarySchema.ReadFromBytes(bytes);
        var rows = new BinaryParser.BinaryReader().ReadFromBytes(bytes);

        return new AiTable
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Source = AiTableSourceKind.Bin,
            Origin = Path.GetFileName(path),
            Columns = schema.Columns.Select(c => AiColumn.From(c.Key, c.Value)).ToList(),
            Rows = rows.Select(r => r.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)).ToList(),
        };
    }

    // ───────────── 공통 ─────────────

    private static AiToolException NotFound(string tableName, string source, IEnumerable<string> known, string? extra)
    {
        var names = known.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var similar = names.Where(n => n.Contains(tableName, StringComparison.OrdinalIgnoreCase)
                                       || tableName.Contains(n, StringComparison.OrdinalIgnoreCase)).ToList();
        var candidates = (similar.Count > 0 ? similar : names).Take(20);

        var hint = $"{(similar.Count > 0 ? "비슷한 이름" : "있는 테이블")}: {string.Join(", ", candidates)}";
        if (extra != null)
            hint += $" / {extra}";
        return new AiToolException("table_not_found", $"{source} 에서 테이블을 찾을 수 없습니다: {tableName}", hint);
    }

    internal static AiTable FromCustomTable(CustomDataTable custom, AiTableSourceKind kind, string origin)
    {
        var table = new AiTable
        {
            Name = custom.Name,
            Source = kind,
            Origin = origin,
            Columns = custom.Header.Headers.Select(h => AiColumn.From(h.name, h.typeString)).ToList(),
        };

        var failures = 0;
        var rawRows = custom.GetAllRowsData();
        for (int i = 0; i < rawRows.Count; i++)
        {
            var row = new Dictionary<string, object?>();
            foreach (var column in table.Columns)
            {
                rawRows[i].TryGetValue(column.Name, out var raw);
                row[column.Name] = NormalizeLikeBinaryWriter(raw, column.Type, out var failed);
                if (!failed)
                    continue;

                if (++failures <= MaxConversionWarnings)
                    table.Warnings.Add($"행 {i} '{column.Name}': '{raw}' 를 {column.Type} 로 바꿀 수 없습니다 — 추출 시 문자열로 기록되어 .bin 이 깨질 수 있습니다.");
            }
            table.Rows.Add(row);
        }

        if (failures > MaxConversionWarnings)
            table.Warnings.Add($"타입 변환 실패가 {failures - MaxConversionWarnings}건 더 있습니다.");

        return table;
    }

    /// <summary>
    /// 원본 셀 문자열 하나를 FromCustomTable 이 내는 값과 같게 바꾼다.
    /// CustomDataTable 이 배열 셀에 하는 변환(DataTypeHelper.ConvertValue) 뒤 NormalizeLikeBinaryWriter 를 거친다.
    /// </summary>
    internal static object? NormalizeCellText(string text, AiColumn column)
    {
        object value = column.IsArray
            ? DataTypeHelper.ConvertValue(text, column.DataType, column.EnumType, isArray: true)
            : text;
        return NormalizeLikeBinaryWriter(value, column.Type, out _);
    }

    /// <summary>
    /// BinaryParser.BinaryWriter.WriteValueByTypeString 과 같은 규칙으로 값을 바꾼다.
    /// 그래서 원본에서 읽은 행이 추출 후 .bin 에 들어갈 값과 같아진다. 변환에 실패하면 writer 처럼 문자열을 돌려준다.
    /// </summary>
    private static object? NormalizeLikeBinaryWriter(object? value, string typeStr, out bool failed)
    {
        failed = false;
        if (value == null)
            return null;

        if (typeStr.EndsWith("[]"))
        {
            var elementType = typeStr[..^2];
            if (value is not IEnumerable enumerable)
                return Array.Empty<object?>(); // writer 는 길이 0 으로 기록한다

            var elements = new List<object?>();
            foreach (var element in enumerable)
            {
                if (element == null)
                {
                    elements.Add(null);
                    continue;
                }

                elements.Add(NormalizeLikeBinaryWriter(element, elementType, out var elementFailed));
                failed |= elementFailed;
            }
            return elements.ToArray();
        }

        try
        {
            return typeStr switch
            {
                "Int16" => Convert.ToInt16(value),
                "Int32" => Convert.ToInt32(value),
                "Int64" => Convert.ToInt64(value),
                "Single" => Convert.ToSingle(value),
                "Double" => Convert.ToDouble(value),
                "Boolean" => Convert.ToBoolean(value),
                "DateTime" => Convert.ToDateTime(value),
                _ => value.ToString() ?? string.Empty // String, 사용자 enum, 기타
            };
        }
        catch (Exception)
        {
            failed = true;
            return value.ToString() ?? string.Empty;
        }
    }
}

using TableAutomationTool.GAuth;
using TableAutomationTool.Shared;

namespace TableAutomationTool.AiTools;

/// <summary>
/// 새 테이블 탭을 만드는 툴(add-sheet·add-file)의 공통 부분: 탭 이름·컬럼 검사, 테이블 이름 중복 검사, 헤더 2행 작성, 다시 읽어 검증.
/// 새 탭에는 1행 컬럼 이름, 2행 타입만 쓴다. 데이터 행은 이후 ai insert 로 넣는다.
/// </summary>
public abstract class AiCreateTableCommand : AiGSheetWriteCommand
{
    protected AiCreateTableCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "sheet", "column", "dry-run" };
    public override int MaxPositionals => 1;

    /// <summary>
    /// 공통 검사 → prepare(대상 스프레드시트 확인, 탭 좌표 준비) → 테이블 이름 검사 → (드라이런이 아니면) create → 다시 읽어 검증.
    /// prepare 는 탭 이름을 받아 SpreadsheetName 등을 채운 탭을 돌려주고, create 는 탭과 헤더 2행을 받아 실제로 만든 탭을 돌려준다.
    /// </summary>
    protected void Run(string spreadsheetName, Func<string, GSheetTab> prepare,
                       Func<GSheetTab, List<IReadOnlyList<string>>, GSheetTab> create, out string result)
    {
        var dryRun = Args.Has("dry-run");
        var sheetTitle = RequireSheetTitle();

        WarnIfNotGSheetSource();
        Ctx.EnsureGSheetReady();

        // enum 타입 컬럼 검사(NormalizeType)가 EnumDB 를 쓴다
        new LoadEnumDBCommand().Execute(out _);
        var columns = ParseColumns();

        var tableName = GAuthService.ToTableName($"{spreadsheetName}_{sheetTitle}");
        AiGSheetTable.RequireIdentifier(tableName, $"테이블 이름 '{tableName}'",
            "테이블 이름은 '<파일>_<탭>' 에서 파일 이름의 첫 '_' 앞 토막을 뗀 것이고 .bin·생성 코드 이름이 됩니다. 파일 이름에 '_' 가 없으면 탭 이름 그대로입니다.");

        var tab = prepare(sheetTitle) with { SheetTitle = sheetTitle, TableName = tableName };

        // 추출기는 같은 테이블 이름의 탭을 같은 .bin 에 차례로 덮어쓴다
        var existing = GAuthService.GetTabsFromGSheet(Ctx.Extract.SrcGDriveLink)
            .Where(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (existing.Count > 0)
            throw new AiToolException("already_exists",
                $"같은 이름의 테이블이 이미 있습니다: {tableName} ({string.Join(", ", existing.Select(AiTableSource.GSheetOrigin))})",
                "다른 탭 이름을 쓰세요. 기존 테이블에 컬럼을 더하려면 ai add-column 을 쓰세요.");
        if (AiTableSource.GetBinNames(Ctx).Contains(tableName))
            Warnings.Add($"추출 폴더에 같은 이름의 {tableName}.bin 이 이미 있습니다(다른 소스에서 추출됐을 수 있습니다). 추출하면 덮어씁니다.");

        var expectedColumns = columns.Select(c => AiColumn.From(c.Name, c.Type)).ToList();
        var headerRows = new List<IReadOnlyList<string>>
        {
            columns.Select(c => c.Name).ToList(),
            columns.Select(c => c.Type).ToList(),
        };

        var applied = false;
        bool? verified = null;
        List<object>? mismatches = null;
        if (!dryRun)
        {
            tab = create(tab, headerRows);
            applied = true;

            mismatches = Verify(tab, expectedColumns, new List<Dictionary<string, object?>>());
            verified = mismatches.Count == 0;
            if (mismatches.Count > 0)
                FailVerify();
        }

        Data = new
        {
            table = tableName,
            source = "gsheet",
            origin = AiTableSource.GSheetOrigin(tab),
            url = applied ? $"https://docs.google.com/spreadsheets/d/{tab.SpreadsheetId}/edit#gid={tab.SheetId}" : null,
            dryRun,
            applied,
            verified,
            columns = columns.Select(c => new { name = c.Name, type = c.Type }),
            mismatches,
        };
        result = $"[{GetType().Name}] {tableName}: {(dryRun ? "dry-run" : "applied")}, {columns.Count} column(s)";
    }

    private string RequireSheetTitle()
    {
        var title = Args.Get("sheet") ?? throw new AiToolException("invalid_args", "탭 이름을 --sheet 이름 으로 지정하세요.");
        if (string.IsNullOrWhiteSpace(title) || title != title.Trim())
            throw new AiToolException("invalid_value", $"탭 이름이 비었거나 앞뒤에 공백이 있습니다: '{title}'");
        if (title.StartsWith("#") || title.StartsWith("//"))
            throw new AiToolException("invalid_value", $"'#'·'//' 로 시작하는 탭은 추출에서 제외되어 테이블이 되지 않습니다: '{title}'");
        return title;
    }

    /// <summary>--column "이름:타입" 목록. 첫 ':' 앞이 이름, 뒤가 타입이다. 순서대로 A열부터 놓는다.</summary>
    private List<(string Name, string Type)> ParseColumns()
    {
        var columns = new List<(string Name, string Type)>();
        foreach (var expression in Args.GetAll("column"))
        {
            var colon = expression.IndexOf(':');
            if (colon <= 0)
                throw new AiToolException("invalid_args", $"--column 형식이 잘못되었습니다: {expression}", "--column \"이름:타입\" (예: \"Id:Int32\")");

            var name = expression[..colon].Trim();
            AiGSheetTable.RequireIdentifier(name, "컬럼 이름", "이름은 생성 코드(Template)의 프로퍼티 이름이 됩니다.");
            if (columns.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new AiToolException("invalid_value", $"같은 이름의 컬럼을 두 번 지정했습니다: {name}");

            columns.Add((name, AiGSheetTable.NormalizeType(expression[(colon + 1)..])));
        }

        if (columns.Count == 0)
            throw new AiToolException("invalid_args", "컬럼을 --column \"이름:타입\" 으로 하나 이상 지정하세요.",
                "보통 첫 컬럼이 키(Id)입니다. 예: --column \"Id:Int32\" --column \"Name:String\"");
        if (columns.Count > AiGSheetTable.MaxSheetColumns)
            throw new AiToolException("column_limit", $"컬럼이 {columns.Count}개입니다. 리더는 A:Z({AiGSheetTable.MaxSheetColumns}열)만 읽습니다.");
        return columns;
    }
}

/// <summary>
/// ai add-sheet &lt;spreadsheet&gt; — 폴더의 기존 스프레드시트에 테이블 탭을 추가한다. 탭 추가와 헤더 쓰기는 batchUpdate 한 번이다.
/// </summary>
public sealed class AiAddSheetCommand : AiCreateTableCommand
{
    public AiAddSheetCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    protected override void ExecuteWrite(out string result)
    {
        var spreadsheetName = Args.RequirePositional(0, "스프레드시트(파일) 이름");

        Run(spreadsheetName, prepare: title =>
        {
            var file = FindSpreadsheet(spreadsheetName);
            var sheets = GAuthService.GetGSheetProperties(file.Id);
            // 제외 탭('#')도 이름이 겹치면 API 가 거절하므로 모든 탭과 비교한다
            var same = sheets.FirstOrDefault(s => s.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            if (same != null)
                throw new AiToolException("already_exists", $"{file.Name} 에 같은 이름의 탭이 이미 있습니다: {same.Title}");

            // batchUpdate 한 번에 탭 추가와 헤더 쓰기를 함께 보내려면 sheetId 를 미리 정해야 한다
            var ids = sheets.Select(s => s.SheetId).ToHashSet();
            int sheetId;
            do sheetId = Random.Shared.Next(1, int.MaxValue);
            while (ids.Contains(sheetId));

            return new GSheetTab { SpreadsheetName = file.Name, SpreadsheetId = file.Id, SheetId = sheetId };
        }, create: (tab, headerRows) =>
        {
            Send(() => GAuthService.AddGSheetTab(tab, headerRows));
            return tab;
        }, out result);
    }

    private Google.Apis.Drive.v3.Data.File FindSpreadsheet(string name)
    {
        var files = GAuthService.GetAllGoogleSheets(Ctx.Extract.SrcGDriveLink);
        var matches = files.Where(f => f.Name == name).ToList();
        if (matches.Count == 0)
            matches = files.Where(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count == 0)
            throw new AiToolException("spreadsheet_not_found", $"GSheet 폴더에 스프레드시트가 없습니다: {name}",
                $"있는 파일: {string.Join(", ", files.Select(f => f.Name).Take(30))} / 새 파일이면 ai add-file 을 쓰세요.");
        if (matches.Count > 1)
            throw new AiToolException("ambiguous_spreadsheet", $"같은 이름의 스프레드시트가 {matches.Count}개라 어디에 추가할지 정할 수 없습니다: {name}",
                "사용자에게 파일 이름 중복을 정리해 달라고 요청하세요.");
        return matches[0];
    }
}

/// <summary>
/// ai add-file &lt;spreadsheet&gt; — 폴더에 새 스프레드시트 파일을 만들고 기본 탭을 테이블 탭으로 만든다.
/// 파일 생성(드라이브)과 탭 쓰기(시트)는 따로 보내므로, 탭 쓰기가 실패하면 만든 파일을 휴지통으로 옮긴다(빈 '시트1' 탭이 폴더에 남지 않도록).
/// 드라이브 쓰기 범위가 필요하다(없으면 gsheet_scope_insufficient).
/// </summary>
public sealed class AiAddFileCommand : AiCreateTableCommand
{
    public AiAddFileCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    protected override void ExecuteWrite(out string result)
    {
        var fileName = Args.RequirePositional(0, "새 스프레드시트(파일) 이름");
        if (fileName != fileName.Trim())
            throw new AiToolException("invalid_value", $"파일 이름 앞뒤에 공백이 있습니다: '{fileName}'");

        Run(fileName, prepare: _ =>
        {
            var same = GAuthService.GetAllGoogleSheets(Ctx.Extract.SrcGDriveLink)
                .FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (same != null)
                throw new AiToolException("already_exists", $"GSheet 폴더에 같은 이름의 스프레드시트가 이미 있습니다: {same.Name}",
                    "기존 파일에 탭을 더하려면 ai add-sheet 를 쓰세요.");
            return new GSheetTab { SpreadsheetName = fileName };
        }, create: (tab, headerRows) =>
        {
            var file = Send(() => GAuthService.CreateGoogleSheet(Ctx.Extract.SrcGDriveLink, fileName));

            try
            {
                // 새 파일의 기본 탭('시트1' 등)을 테이블 탭으로 바꾼다
                var first = GAuthService.GetGSheetProperties(file.Id)[0];
                tab = tab with { SpreadsheetId = file.Id, SheetId = first.SheetId };
                Send(() => GAuthService.InitGSheetTab(tab, headerRows));
                return tab;
            }
            catch (Exception ex)
            {
                var error = ex as AiToolException ?? new AiToolException("gsheet_api_error", ex.Message);
                string cleanup;
                try
                {
                    GAuthService.TrashFile(file.Id);
                    cleanup = "만든 파일은 휴지통으로 옮겼습니다.";
                }
                catch (Exception trashEx)
                {
                    cleanup = $"만든 빈 파일을 휴지통으로 옮기지 못했습니다({trashEx.Message}). 사용자에게 지워 달라고 요청하세요: {file.Link}";
                }
                throw new AiToolException(error.Code, $"파일은 만들었지만 탭을 채우지 못했습니다: {error.Message} {cleanup}", error.Hint);
            }
        }, out result);
    }
}

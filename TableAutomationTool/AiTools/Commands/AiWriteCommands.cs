using System.Globalization;
using TableAutomationTool.GAuth;
using TableAutomationTool.Shared;
using TableAutomationTool.UserEnum;

namespace TableAutomationTool.AiTools;

/// <summary>
/// GSheet 쓰기 툴(update·insert·delete)의 공통 흐름:
/// 탭 찾기 → 원시 값 읽기·좌표 계산(AiGSheetTable) → 편집·기대 테이블 계산(Plan) → (--dry-run 이 아니면) batchUpdate 한 번 → 다시 읽어 검증.
/// 쓰기 대상은 항상 GSheet 다(--source 를 받지 않는다). 로컬 xlsx·.bin 은 건드리지 않는다.
/// </summary>
public abstract class AiWriteCommand : AiCommand
{
    private const int MaxListedRows = 20;

    // 시트 쓰기 시작 여부. AiToolRunner 의 시간 초과와 경합하므로 Interlocked 로 바꾼다.
    // 0 = 아직, 1 = batchUpdate 를 보냄, 2 = 시간 초과로 막힘(이후 쓰지 않는다)
    private static int _applyState;

    /// <summary>
    /// 시간 초과 시 AiToolRunner 가 부른다. 쓰기를 아직 시작하지 않았으면 막고 true(결과를 버려도 시트는 그대로),
    /// 이미 batchUpdate 를 보냈으면 false(결과를 버리면 적용 여부를 알 수 없으므로 끝까지 기다려야 한다).
    /// </summary>
    internal static bool TryBlockApply() => Interlocked.CompareExchange(ref _applyState, 2, 0) != 1;

    protected sealed class WritePlan
    {
        /// <summary>순서대로 적용할 편집 (GAuthService.EditGSheetTab)</summary>
        public List<GSheetEdit> Edits { get; } = new();

        /// <summary>결과로 돌려줄 변경 내역</summary>
        public List<object> Changes { get; } = new();

        /// <summary>적용 후 리더가 읽어야 할 테이블 전체</summary>
        public List<Dictionary<string, object?>> Expected { get; init; } = new();

        /// <summary>Expected 에서 이번에 값이 바뀌거나 생긴 행 (키 중복 검사 대상)</summary>
        public List<int> TouchedRows { get; } = new();

        /// <summary>이미 같은 값이라 쓰지 않은 셀 수 (update 만)</summary>
        public int? Unchanged { get; set; }
    }

    protected AiWriteCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override int MaxPositionals => 1;

    protected abstract WritePlan Plan(AiGSheetTable sheet);

    public override void Execute(out string result)
    {
        var name = Args.RequirePositional(0, "테이블 이름");
        var dryRun = Args.Has("dry-run");

        if (!Ctx.Extract.IsLoadFromGSheet)
            Warnings.Add("설정상 추출 소스가 GSheet 가 아닙니다(IsLoadFromGSheet=false). 이 수정은 추출 결과에 반영되지 않습니다.");

        // enum 값 검사와 리더의 배열 enum 변환이 EnumDB 를 쓴다. 쓰기 전후 읽기가 같은 EnumDB 상태에서 돌도록 먼저 불러 둔다.
        new LoadEnumDBCommand().Execute(out var enumResult);
        if (EnumDB.Instance.IsEmpty)
            Warnings.Add($"EnumDB 를 불러오지 못해 enum 값을 검사하지 못했습니다({enumResult}). 'ai run enum' 으로 만들 수 있습니다.");

        // 추출기는 같은 이름의 탭을 차례로 같은 .bin 에 덮어써 마지막 탭이 남는다. 어느 탭이 원본인지 정할 수 없으므로 쓰지 않는다.
        var matches = AiTableSource.FindGSheetTabs(Ctx, name);
        if (matches.Count > 1)
            throw new AiToolException("ambiguous_table",
                $"같은 이름의 탭이 여러 개라 어느 탭을 고칠지 정할 수 없습니다: {string.Join(", ", matches.Select(AiTableSource.GSheetOrigin))}",
                "추출은 같은 이름 .bin 을 차례로 덮어써 마지막 탭이 남습니다. 사용자에게 탭 이름 중복을 정리해 달라고 요청하세요.");
        var tab = matches[0];

        var sheet = AiGSheetTable.Load(tab);
        var plan = Plan(sheet);
        CheckKeyDuplicates(sheet, plan);

        var applied = false;
        bool? verified = null;
        List<object>? mismatches = null;
        if (!dryRun && plan.Edits.Count > 0)
        {
            Apply(tab, plan.Edits);
            applied = true;

            mismatches = Verify(tab, plan.Expected);
            verified = mismatches.Count == 0;
            if (mismatches.Count > 0)
            {
                Succeeded = false;
                FailureCode = "verify_failed";
                FailureMessage = "시트에 썼지만 다시 읽은 값이 기대와 다릅니다. data.mismatches 를 확인하세요"
                                 + "(셀 표시 형식이 값을 바꿨거나 다른 편집이 동시에 있었을 수 있습니다).";
            }
        }

        Data = new
        {
            table = sheet.Table.Name,
            source = "gsheet",
            origin = sheet.Origin,
            dryRun,
            applied,
            verified,
            edits = plan.Edits.Count,
            unchanged = plan.Unchanged,
            changes = plan.Changes,
            mismatches,
        };
        result = $"[{GetType().Name}] {sheet.Table.Name}: {(dryRun ? "dry-run" : applied ? "applied" : "no change")}, {plan.Edits.Count} edit(s)";
    }

    private static void Apply(GSheetTab tab, IReadOnlyList<GSheetEdit> edits)
    {
        if (Interlocked.CompareExchange(ref _applyState, 1, 0) != 0)
            throw new AiToolException("timeout", "시간 초과로 시트 쓰기를 시작하지 않았습니다. 시트는 바뀌지 않았습니다.");

        try
        {
            GAuthService.EditGSheetTab(tab, edits);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new AiToolException("gsheet_permission_denied", $"시트를 수정할 권한이 없습니다: {ex.Message}",
                "로그인한 Google 계정에 이 스프레드시트 편집 권한이 있는지 확인하세요. 토큰 범위가 부족하면 사용자에게 TableEditor 설정 탭의 [Google 인증]을 다시 요청하세요.");
        }
    }

    /// <summary>다시 읽어 기대 테이블과 비교한다. 쓰기는 이미 끝났으므로 다시 읽기 실패도 예외 대신 불일치로 돌려준다.</summary>
    private static List<object> Verify(GSheetTab tab, List<Dictionary<string, object?>> expected)
    {
        try
        {
            return AiGSheetTable.Diff(expected, AiGSheetTable.Load(tab));
        }
        catch (Exception ex)
        {
            return new List<object> { new { reason = "reread_failed", message = $"{ex.GetType().Name}: {ex.Message}" } };
        }
    }

    /// <summary>
    /// 클라이언트 Info 핸들러는 보통 첫 컬럼(Id)을 키로 쓴다. 이번에 바뀌거나 생긴 행의 첫 컬럼 값이 다른 행과 겹치면 경고한다.
    /// </summary>
    private void CheckKeyDuplicates(AiGSheetTable sheet, WritePlan plan)
    {
        var key = sheet.Table.Columns.FirstOrDefault();
        if (key == null)
            return;

        foreach (var r in plan.TouchedRows.Distinct())
        {
            var value = AiRowFilter.Format(plan.Expected[r].GetValueOrDefault(key.Name));
            var others = plan.Expected
                .Select((row, i) => (row, i))
                .Where(x => x.i != r && AiRowFilter.Format(x.row.GetValueOrDefault(key.Name)) == value)
                .Select(x => x.i)
                .ToList();

            if (others.Count > 0)
                Warnings.Add($"첫 컬럼 '{key.Name}' 값 '{value}' 이(가) 다른 행에도 있습니다(적용 후 _row {JoinRows(others)}). 키 컬럼이면 중복입니다.");
        }
    }

    /// <summary>
    /// --row(= ai rows 의 _row)·--where 로 대상 행을 고른다. 둘 다 주면 교집합이다(--where 가 오래된 _row 를 막는 가드).
    /// --where 만으로 2행 이상이 맞으면 --all 이 있어야 한다.
    /// </summary>
    protected List<int> SelectRows(AiGSheetTable sheet)
    {
        var rowOptions = Args.GetAll("row");
        var conditions = Args.GetAll("where").Select(AiRowFilter.ParseCondition).ToList();
        if (rowOptions.Count == 0 && conditions.Count == 0)
            throw new AiToolException("invalid_args", $"{Args.Tool} 할 행을 --row N 또는 --where \"컬럼=값\" 으로 지정하세요.",
                "ai rows 로 _row 나 키 값을 먼저 확인하세요.");

        var rows = sheet.Table.Rows;
        var columnNames = sheet.Table.Columns.Select(c => c.Name).ToList();
        foreach (var condition in conditions)
            AiRowFilter.RequireColumn(columnNames, condition.Column);

        var candidates = rowOptions.Count == 0
            ? Enumerable.Range(0, rows.Count).ToList()
            : rowOptions.Select(o => ParseRowIndex(o, rows.Count, "--row")).Distinct().OrderBy(i => i).ToList();

        var matched = candidates.Where(i => conditions.All(c => AiRowFilter.Matches(rows[i], c))).ToList();

        // 가드(--where)에 맞지 않는 --row 가 하나라도 있으면 _row 가 낡은 것이다. 맞는 행만 골라 진행하지 않는다.
        if (rowOptions.Count > 0 && conditions.Count > 0 && matched.Count > 0 && matched.Count < candidates.Count)
            throw new AiToolException("no_match",
                $"--row {JoinRows(candidates.Except(matched).ToList())} 이(가) --where 조건과 맞지 않습니다.",
                "ai rows 로 현재 행을 다시 확인하세요(다른 편집으로 행이 밀렸을 수 있습니다).");

        if (matched.Count == 0)
            throw new AiToolException("no_match",
                rowOptions.Count > 0 && conditions.Count > 0 ? "--row 의 행이 --where 조건과 맞지 않습니다." : "조건에 맞는 행이 없습니다.",
                "ai rows 로 현재 행을 다시 확인하세요(다른 편집으로 행이 밀렸을 수 있습니다).");

        if (rowOptions.Count == 0 && matched.Count > 1 && !Args.Has("all"))
            throw new AiToolException("multiple_matches", $"--where 에 맞는 행이 {matched.Count}개입니다: _row {JoinRows(matched)}",
                "모두 대상으로 하려면 --all, 하나만이면 --row 를 함께 주거나 조건을 좁히세요.");

        return matched;
    }

    protected static int ParseRowIndex(string text, int count, string option)
    {
        if (count == 0)
            throw new AiToolException("invalid_args", $"{option}: 데이터 행이 없습니다.");
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= count)
            throw new AiToolException("invalid_args", $"{option} 는 0 ~ {count - 1} 사이의 _row 여야 합니다: {text}");
        return index;
    }

    /// <summary>--set "컬럼=값" 목록. 첫 '=' 앞이 컬럼 이름(대소문자 무시), 뒤 전체가 값이다.</summary>
    protected List<(AiColumn column, string value)> ParseSets(AiGSheetTable sheet)
    {
        var columnNames = sheet.Table.Columns.Select(c => c.Name).ToList();
        var sets = new List<(AiColumn column, string value)>();

        foreach (var expression in Args.GetAll("set"))
        {
            var eq = expression.IndexOf('=');
            if (eq <= 0)
                throw new AiToolException("invalid_args", $"--set 형식이 잘못되었습니다: {expression}", "--set \"컬럼=값\"");

            var name = AiRowFilter.RequireColumn(columnNames, expression[..eq].Trim());
            if (sets.Any(s => s.column.Name == name))
                throw new AiToolException("invalid_args", $"같은 컬럼을 두 번 지정했습니다: {name}");

            sets.Add((sheet.Table.Columns.First(c => c.Name == name), expression[(eq + 1)..]));
        }
        return sets;
    }

    private static string JoinRows(IReadOnlyCollection<int> rows)
        => string.Join(", ", rows.Take(MaxListedRows)) + (rows.Count > MaxListedRows ? ", …" : string.Empty);
}

/// <summary>
/// ai update &lt;table&gt; — 대상 행의 셀 값을 바꾼다. 이미 같은 값인 셀은 쓰지 않는다.
/// </summary>
public sealed class AiUpdateCommand : AiWriteCommand
{
    public AiUpdateCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "row", "where", "set", "all", "dry-run" };

    protected override WritePlan Plan(AiGSheetTable sheet)
    {
        var sets = ParseSets(sheet);
        if (sets.Count == 0)
            throw new AiToolException("invalid_args", "바꿀 값을 --set \"컬럼=값\" 으로 지정하세요.");

        var targets = SelectRows(sheet);
        var cells = sets.Select(s => (s.column, value: AiGSheetTable.Encode(s.column, s.value, Warnings))).ToList();

        var plan = new WritePlan { Expected = sheet.CloneRows(), Unchanged = 0 };
        foreach (var r in targets)
        {
            var sheetRow = sheet.DataRows[r];
            var changed = new Dictionary<string, object>();
            var writes = new List<(int column, string text)>();

            foreach (var (column, value) in cells)
            {
                var before = sheet.Table.Rows[r][column.Name];
                if (AiRowFilter.Format(before) == AiRowFilter.Format(value.Expected))
                {
                    plan.Unchanged++;
                    continue;
                }

                var sheetColumn = sheet.SheetColumnOf(column.Name);
                plan.Edits.Add(new GSheetSetCell(sheetRow, sheetColumn, value.SheetValue));
                writes.Add((sheetColumn, value.Text));
                plan.Expected[r][column.Name] = value.Expected;
                changed[column.Name] = new { before, after = value.Expected };
            }

            if (changed.Count == 0)
                continue;

            sheet.EnsureDataRow(sheetRow, writes, $"_row {r}");
            plan.TouchedRows.Add(r);
            plan.Changes.Add(new { _row = r, sheetRow = sheetRow + 1, cells = changed });
        }
        return plan;
    }
}

/// <summary>
/// ai insert &lt;table&gt; — 시트에 행을 끼워 넣고 값을 쓴다. 기본 위치는 마지막 데이터 행 바로 아래라
/// 그 아래의 EOF·주석 행은 밀려 내려갈 뿐 그대로 남는다.
/// </summary>
public sealed class AiInsertCommand : AiWriteCommand
{
    public AiInsertCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "set", "copy", "at", "dry-run" };

    protected override WritePlan Plan(AiGSheetTable sheet)
    {
        var rows = sheet.Table.Rows;
        var at = Args.Get("at") is { } atText ? ParseInsertIndex(atText, rows.Count) : rows.Count;
        int? copy = Args.Get("copy") is { } copyText ? ParseRowIndex(copyText, rows.Count, "--copy") : null;
        var sets = ParseSets(sheet).ToDictionary(s => s.column.Name, s => s.value);

        // 조용한 기본값을 넣지 않는다: 빈 셀이 허용되는 문자열·배열 외에는 값을 받아야 한다
        if (copy == null)
        {
            var missing = sheet.Table.Columns
                .Where(c => !c.IsArray && c.Type != "String" && !sets.ContainsKey(c.Name))
                .Select(c => c.Name)
                .ToList();
            if (missing.Count > 0)
                throw new AiToolException("missing_value", $"값이 필요한 컬럼이 빠졌습니다: {string.Join(", ", missing)}",
                    "문자열·배열이 아닌 컬럼은 모두 --set 으로 채우거나, 비슷한 행을 --copy N 으로 복사하고 바꿀 컬럼만 --set 하세요.");
        }

        // 대상 데이터 행 앞에 끼우면 그 행(아래)의 서식을, 끝에 붙이면 마지막 데이터 행(위)의 서식을 따른다
        int sheetRow;
        bool inheritFromBefore;
        if (at < rows.Count)
        {
            sheetRow = sheet.DataRows[at];
            inheritFromBefore = false;
        }
        else
        {
            sheetRow = rows.Count > 0 ? sheet.DataRows[^1] + 1 : sheet.TypeRow + 1;
            inheritFromBefore = true;
        }

        var plan = new WritePlan { Expected = sheet.CloneRows() };
        plan.Edits.Add(new GSheetInsertRow(sheetRow, inheritFromBefore));

        var values = new Dictionary<string, object?>();
        var writes = new List<(int column, string text)>();
        foreach (var column in sheet.Table.Columns)
        {
            var sheetColumn = sheet.SheetColumnOf(column.Name);
            var fromSet = sets.TryGetValue(column.Name, out var input);
            if (!fromSet && copy is { } source)
                input = sheet.RawCell(sheet.DataRows[source], sheetColumn);

            if (input == null)
            {
                values[column.Name] = AiTableSource.NormalizeCellText(string.Empty, column);
                continue;
            }

            AiCellValue cell;
            try
            {
                cell = AiGSheetTable.Encode(column, input, Warnings);
            }
            catch (AiToolException ex) when (!fromSet)
            {
                throw new AiToolException(ex.Code, $"--copy {copy} 행의 값을 쓸 수 없습니다: {ex.Message}",
                    $"--set \"{column.Name}=값\" 으로 덮어쓰세요.");
            }

            values[column.Name] = cell.Expected;
            if (cell.Text.Length == 0)
                continue; // 새 행은 비어 있으므로 빈 값은 쓸 필요가 없다

            plan.Edits.Add(new GSheetSetCell(sheetRow, sheetColumn, cell.SheetValue));
            writes.Add((sheetColumn, cell.Text));
        }

        sheet.EnsureDataRow(null, writes, "새 행");
        plan.Expected.Insert(at, values);
        plan.TouchedRows.Add(at);
        plan.Changes.Add(new { _row = at, sheetRow = sheetRow + 1, values });
        return plan;
    }

    private static int ParseInsertIndex(string text, int count)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index > count)
            throw new AiToolException("invalid_args", $"--at 은 0 ~ {count} 사이여야 합니다(새 행의 _row, {count} = 끝): {text}");
        return index;
    }
}

/// <summary>
/// ai delete &lt;table&gt; — 대상 행을 시트에서 지운다. 지운 행의 값은 결과에 남는다(되돌릴 때 insert 로 쓴다).
/// </summary>
public sealed class AiDeleteCommand : AiWriteCommand
{
    public AiDeleteCommand(AiContext ctx, AiArgs args) : base(ctx, args) { }

    public override IReadOnlyCollection<string> Options => new[] { "row", "where", "all", "dry-run" };

    protected override WritePlan Plan(AiGSheetTable sheet)
    {
        var targets = SelectRows(sheet);
        var plan = new WritePlan { Expected = sheet.CloneRows() };

        // 아래 행부터 지워야 앞 편집이 뒤 편집의 좌표를 밀지 않는다
        foreach (var r in targets.OrderByDescending(r => r))
        {
            plan.Edits.Add(new GSheetDeleteRow(sheet.DataRows[r]));
            plan.Expected.RemoveAt(r);
        }

        foreach (var r in targets)
            plan.Changes.Add(new { _row = r, sheetRow = sheet.DataRows[r] + 1, values = sheet.Table.Rows[r] });
        return plan;
    }
}

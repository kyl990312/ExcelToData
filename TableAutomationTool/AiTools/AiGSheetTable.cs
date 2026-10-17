using System.Globalization;
using TableAutomationTool.ExcelParser;
using TableAutomationTool.GAuth;
using TableAutomationTool.UserEnum;

namespace TableAutomationTool.AiTools;

/// <summary>
/// 셀에 쓸 값. SheetValue 는 API 로 보낼 값(string | double | bool), Text 는 시트가 돌려줄 표시 값,
/// Expected 는 그 값을 리더가 읽은 결과(ai rows 에 보일 값)다.
/// </summary>
public sealed record AiCellValue(object SheetValue, string Text, object? Expected);

/// <summary>
/// 쓰기용으로 읽은 GSheet 테이블. 리더(GetTableFromGSheetTab → TableConverter)가 보는 행·열을 시트 좌표와 함께 갖고,
/// 쓸 값을 검사·인코딩한다.
///
/// 행·열 규칙의 원천은 TableConverter 다(CP949 파일이라 손대지 않는다). 여기서는 같은 규칙으로 좌표를 계산한 뒤,
/// 같은 원시 값을 TableHelper.GetTable 에 넣은 결과와 맞춰 보고, 다르면 쓰지 않도록 layout_mismatch 로 멈춘다.
/// </summary>
public sealed class AiGSheetTable
{
    /// <summary>
    /// 숫자 셀로 쓸 정수의 최대 절댓값(14자리). 시트 자동 서식은 큰 정수를 지수로 표시해(999999999999999 → 1E+15,
    /// 9007199254740992 → 9.0072E+15) 리더(표시 값을 읽는다)가 정수로 바꾸지 못한다. 그보다 크면 문자열 셀로 쓴다.
    /// </summary>
    private const long MaxNumberCellInteger = 99_999_999_999_999L;

    private const int MaxMismatches = 20;

    private readonly Dictionary<string, int> _sheetColumns;

    public GSheetTab Tab { get; }

    /// <summary>정규화된 값. ai rows 가 보여 주는 값과 같다.</summary>
    public AiTable Table { get; }

    /// <summary>원시 표시 값. 인덱스 = 시트 행(0부터)</summary>
    public IReadOnlyList<string[]> Values { get; }

    /// <summary>데이터 행(_row) → 시트 행(0부터)</summary>
    public IReadOnlyList<int> DataRows { get; }

    /// <summary>타입 헤더 행(시트 행, 0부터)</summary>
    public int TypeRow { get; }

    public string Origin => Table.Origin;

    private AiGSheetTable(GSheetTab tab, AiTable table, List<string[]> values, List<int> dataRows, int typeRow,
                          Dictionary<string, int> sheetColumns)
    {
        Tab = tab;
        Table = table;
        Values = values;
        DataRows = dataRows;
        TypeRow = typeRow;
        _sheetColumns = sheetColumns;
    }

    public static AiGSheetTable Load(GSheetTab tab) => From(tab, GAuthService.GetGSheetTabValues(tab));

    /// <summary>컬럼의 시트 열(0부터)</summary>
    public int SheetColumnOf(string column) => _sheetColumns[column];

    public string RawCell(int sheetRow, int sheetColumn)
    {
        if (sheetRow < 0 || sheetRow >= Values.Count)
            return string.Empty;
        var row = Values[sheetRow];
        return sheetColumn < row.Length ? row[sheetColumn] ?? string.Empty : string.Empty;
    }

    /// <summary>현재 행 값을 복사한다. 기대 테이블(쓰기 후 리더가 볼 값)을 만들 때 쓴다.</summary>
    public List<Dictionary<string, object?>> CloneRows()
        => Table.Rows.Select(r => new Dictionary<string, object?>(r)).ToList();

    // ───────────── 레이아웃 ─────────────

    private static AiGSheetTable From(GSheetTab tab, List<string[]> values)
    {
        // 행: TableConverter.GetValidRowsFromStringArray 와 같은 규칙.
        // (GetTableFromGSheetTab 이 먼저 하는 빈 행 제거·EOF 중단도 이 규칙 안에 들어 있다)
        var validRows = new List<int>();
        for (int i = 0; i < values.Count; i++)
        {
            var row = values[i];
            if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace))
                continue;

            var first = row[0] ?? string.Empty;
            if (first.Equals("EOF", StringComparison.OrdinalIgnoreCase))
                break;
            if (first.StartsWith(TableDefine.REGEX_START_EXCEPT_ROW))
                continue;

            validRows.Add(i);
        }

        if (validRows.Count < 2)
            throw NoColumns(tab, "이름·타입 헤더 2행이 없습니다");

        // 열: TableConverter.GetValidColumns 와 같은 규칙 (빈 이름·'#' 시작·중복 이름 제외. 중복은 대소문자를 구분한다)
        var names = values[validRows[0]];
        var columns = new List<(string name, int index)>();
        var sheetColumns = new Dictionary<string, int>();
        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i] ?? string.Empty;
            if (string.IsNullOrEmpty(name) || name.StartsWith(TableDefine.REGEX_START_EXCEPT_COLUMN) || sheetColumns.ContainsKey(name))
                continue;

            sheetColumns[name] = i;
            columns.Add((name, i));
        }

        if (columns.Count == 0)
            throw NoColumns(tab, "유효한 컬럼이 없습니다");

        var dataRows = validRows.Skip(2).ToList();
        var custom = TableHelper.GetTable(tab.TableName, values);
        CrossCheck(custom, values, columns, dataRows);

        var table = AiTableSource.FromCustomTable(custom, AiTableSourceKind.GSheet, AiTableSource.GSheetOrigin(tab));
        return new AiGSheetTable(tab, table, values, dataRows, validRows[1], sheetColumns);
    }

    /// <summary>
    /// 계산한 좌표가 리더 결과와 같은지 본다: 컬럼 이름·순서, 행 수, 배열이 아닌 셀의 원시 값.
    /// (배열 셀은 CustomDataTable 이 값을 변환하므로 원시 문자열로 비교할 수 없다)
    /// </summary>
    private static void CrossCheck(CustomDataTable custom, List<string[]> values, List<(string name, int index)> columns, List<int> dataRows)
    {
        var names = columns.Select(c => c.name).ToList();
        if (!custom.ColumnNames.SequenceEqual(names))
            throw Mismatch($"컬럼 — 계산 [{string.Join(", ", names)}] / 리더 [{string.Join(", ", custom.ColumnNames)}]");

        if (custom.RowCount != dataRows.Count)
            throw Mismatch($"데이터 행 수 — 계산 {dataRows.Count} / 리더 {custom.RowCount}");

        var arrayColumns = custom.Header.Headers.Where(h => h.isArray).Select(h => h.name).ToHashSet();
        for (int r = 0; r < dataRows.Count; r++)
        {
            var row = values[dataRows[r]];
            foreach (var (name, index) in columns.Where(c => !arrayColumns.Contains(c.name)))
            {
                var raw = index < row.Length ? row[index] ?? string.Empty : string.Empty;
                var official = custom.GetValue(name, r)?.ToString() ?? string.Empty;
                if (raw != official)
                    throw Mismatch($"행 {r} '{name}' — 계산 '{raw}' / 리더 '{official}'");
            }
        }
    }

    private static AiToolException Mismatch(string detail)
        => new("layout_mismatch", $"쓰기 좌표 계산이 리더 결과와 다릅니다({detail}). 아무것도 쓰지 않았습니다.",
            "TableConverter 의 행·열 규칙이 바뀌었으면 AiGSheetTable 의 규칙을 맞추세요.");

    private static AiToolException NoColumns(GSheetTab tab, string reason)
        => new("no_columns", $"{AiTableSource.GSheetOrigin(tab)}: {reason}. 테이블로 읽을 수 없어 쓰지 않습니다.",
            "1행에 컬럼 이름, 2행에 타입이 있어야 합니다('//' 행·빈 행 제외).");

    // ───────────── 편집 후 행 검사 ─────────────

    /// <summary>
    /// 편집 뒤에도 리더가 이 행을 데이터 행으로 읽는지 본다. 빈 행이 되거나 A열이 '//'·EOF 가 되면
    /// 행이 사라지거나 그 아래가 잘리므로 쓰지 않는다.
    /// </summary>
    public void EnsureDataRow(int? sheetRow, IEnumerable<(int column, string text)> writes, string what)
    {
        var texts = sheetRow is { } r && r < Values.Count ? Values[r].ToList() : new List<string>();
        foreach (var (column, text) in writes)
        {
            while (texts.Count <= column)
                texts.Add(string.Empty);
            texts[column] = text;
        }

        if (texts.All(string.IsNullOrWhiteSpace))
            throw new AiToolException("invalid_value", $"{what}: 모든 셀이 비어 리더가 행을 건너뜁니다.", "값을 하나 이상 넣으세요.");

        var first = texts.Count > 0 ? texts[0] ?? string.Empty : string.Empty;
        if (first.Equals("EOF", StringComparison.OrdinalIgnoreCase) || first.StartsWith(TableDefine.REGEX_START_EXCEPT_ROW))
            throw new AiToolException("invalid_value",
                $"{what}: A열 값 '{first}' 는 EOF·주석('//') 표시라 리더가 행을 건너뛰거나 그 아래를 읽지 않습니다.");
    }

    // ───────────── 값 검사·인코딩 ─────────────

    /// <summary>
    /// 컬럼에 쓸 입력 문자열을 검사하고 시트 값으로 바꾼다. 추출을 깨뜨릴 값(빈 숫자·변환 불가·enum 멤버 아님)이면 invalid_value.
    /// 숫자는 numberValue, 나머지는 stringValue(수식 해석 없음)로 쓴다.
    /// </summary>
    public static AiCellValue Encode(AiColumn column, string input, List<string> warnings)
    {
        if (!column.IsArray)
        {
            var cell = EncodeScalar(column, column.Type, input, inArray: false, warnings);
            return cell with { Expected = AiTableSource.NormalizeCellText(cell.Text, column) };
        }

        // 요소 분리는 DataTypeHelper.ConvertToArray 와 같다. 리더는 변환 실패 요소를 조용히 0 으로 바꾸므로 여기서 요소마다 검사한다.
        var elementType = column.Type[..^2];
        var elements = input.Trim().Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => EncodeScalar(column, elementType, e.Trim().Trim('"'), inArray: true, warnings).Text)
            .ToList();

        var text = elements.Count == 0 ? string.Empty : $"[{string.Join(",", elements)}]";
        return new AiCellValue(text, text, AiTableSource.NormalizeCellText(text, column));
    }

    private static AiCellValue EncodeScalar(AiColumn column, string type, string input, bool inArray, List<string> warnings)
    {
        if (type == "String")
            return new AiCellValue(input, input, null);

        var text = input.Trim();
        if (text.Length == 0)
            throw Invalid(column, input, "빈 값은 쓸 수 없습니다(추출 시 타입 변환·Enum.Parse 가 실패합니다)");

        switch (type)
        {
            case "Int16":
            case "Int32":
            case "Int64":
            {
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) || !FitsInteger(type, integer))
                    throw Invalid(column, input, $"{type} 정수가 아닙니다");

                var integerText = integer.ToString(CultureInfo.InvariantCulture);
                // 15자리 이상은 문자열로 쓴다(리더는 문자열도 Convert.ToInt64 로 읽는다)
                return integer is >= -MaxNumberCellInteger and <= MaxNumberCellInteger
                    ? new AiCellValue((double)integer, integerText, null)
                    : new AiCellValue(integerText, integerText, null);
            }
            case "Single":
            case "Double":
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)
                    || !double.IsFinite(real) || (type == "Single" && !float.IsFinite((float)real)))
                    throw Invalid(column, input, $"{type} 실수가 아닙니다(소수점은 '.')");

                return new AiCellValue(real, real.ToString("R", CultureInfo.InvariantCulture), null);
            }
            case "Boolean":
            {
                if (!bool.TryParse(text, out var flag))
                    throw Invalid(column, input, "true | false 가 아닙니다");
                return new AiCellValue(flag, flag ? "TRUE" : "FALSE", null);
            }
            case "DateTime":
            {
                // 리더는 Convert.ToDateTime(현재 문화권)으로 읽는다
                if (!DateTime.TryParse(text, out _))
                    throw Invalid(column, input, "날짜로 읽을 수 없습니다");
                return new AiCellValue(text, text, null);
            }
            default:
                return EncodeEnum(column, type, text, inArray, warnings);
        }
    }

    /// <summary>
    /// 클라이언트는 enum 셀을 Enum.Parse(ignoreCase) 로 읽으므로 멤버 이름(선언된 대소문자로 맞춘다)이나 정수만 받는다.
    /// 배열 요소의 정수는 리더(ConvertToUserEnumString)가 멤버 이름으로 바꾸고 없는 값이면 빈 문자열로 만들므로, 정의된 값만 받아 이름으로 쓴다.
    /// </summary>
    private static AiCellValue EncodeEnum(AiColumn column, string enumType, string text, bool inArray, List<string> warnings)
    {
        if (!EnumDB.Instance.HasEnum(enumType))
        {
            var warning = $"'{column.Name}' 의 타입 '{enumType}' 이 EnumDB 에 없어 값을 검사하지 않았습니다.";
            if (!warnings.Contains(warning))
                warnings.Add(warning);
            return new AiCellValue(text, text, null);
        }

        var members = EnumDB.Instance.GetEnumMembers(enumType);
        var member = members.FirstOrDefault(m => m.member.Equals(text, StringComparison.OrdinalIgnoreCase)).member;
        if (member != null)
            return new AiCellValue(member, member, null);

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            if (!inArray)
                return new AiCellValue(text, text, null);

            var name = EnumDB.Instance.GetEnumMemberName(enumType, number);
            if (!string.IsNullOrEmpty(name))
                return new AiCellValue(name, name, null);
        }

        throw Invalid(column, text, $"{enumType} 멤버가 아닙니다",
            $"멤버: {string.Join(", ", members.Select(m => $"{m.member}={m.value}"))}");
    }

    private static bool FitsInteger(string type, long value) => type switch
    {
        "Int16" => value is >= short.MinValue and <= short.MaxValue,
        "Int32" => value is >= int.MinValue and <= int.MaxValue,
        _ => true
    };

    private static AiToolException Invalid(AiColumn column, string input, string reason, string? hint = null)
        => new("invalid_value", $"'{column.Name}'({column.Type}) 에 '{input}' 를 쓸 수 없습니다: {reason}", hint);

    // ───────────── 검증 ─────────────

    /// <summary>
    /// 기대 테이블과 다시 읽은 테이블을 비교한다. 같은 Format(InvariantCulture) 문자열이면 같은 값으로 본다.
    /// 다른 셀은 최대 MaxMismatches 개까지 돌려준다.
    /// </summary>
    public static List<object> Diff(IReadOnlyList<Dictionary<string, object?>> expected, AiGSheetTable actual)
    {
        var mismatches = new List<object>();
        var rows = actual.Table.Rows;
        if (expected.Count != rows.Count)
            mismatches.Add(new { reason = "rowCount", expected = expected.Count, actual = rows.Count });

        for (int r = 0; r < Math.Min(expected.Count, rows.Count) && mismatches.Count < MaxMismatches; r++)
        {
            foreach (var column in actual.Table.Columns)
            {
                expected[r].TryGetValue(column.Name, out var e);
                rows[r].TryGetValue(column.Name, out var a);
                if (AiRowFilter.Format(e) == AiRowFilter.Format(a))
                    continue;

                mismatches.Add(new { _row = r, column = column.Name, expected = e, actual = a });
                if (mismatches.Count >= MaxMismatches)
                    break;
            }
        }
        return mismatches;
    }
}

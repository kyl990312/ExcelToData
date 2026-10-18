using System.Data;
using TableAutomationTool.Common.Utils;
using TableAutomationTool.GAuth.Core;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace TableAutomationTool.GAuth;

public struct GSheetTable
{
    public string TableName { get; set; }
    public List<string[]> Data { get; set; }
    public string SpreadsheetId { get; set; }
    public string SheetTitle { get; set; }
}

/// <summary>
/// 데이터 없이 탭 메타데이터만 담는다. 목록 조회에서 전체 데이터를 내려받지 않기 위해 쓴다.
/// </summary>
public struct GSheetTab
{
    public string SpreadsheetName { get; set; }
    public string SpreadsheetId { get; set; }
    public string SheetTitle { get; set; }

    /// <summary>탭의 sheetId(gid). 행 삽입·삭제·셀 쓰기(batchUpdate)가 탭을 가리킬 때 쓴다.</summary>
    public int SheetId { get; set; }

    /// <summary>추출 결과 .bin 이름과 같은 테이블 이름 (ToTableName 규칙)</summary>
    public string TableName { get; set; }
}

/// <summary>
/// EditGSheetTab 이 한 번의 batchUpdate 로 순서대로 적용하는 편집. 행·열은 0부터 센 시트 좌표다.
/// 앞 편집이 행을 끼우거나 지우면 뒤 편집의 좌표는 그 결과를 기준으로 한다.
/// </summary>
public abstract record GSheetEdit;

/// <summary>Row 위치에 빈 행을 끼운다. 기존 Row 이하 행은 한 칸씩 밀린다. InheritFromBefore 면 위 행, 아니면 아래 행의 서식을 따른다.</summary>
public sealed record GSheetInsertRow(int Row, bool InheritFromBefore) : GSheetEdit;

/// <summary>Row 행을 지운다. 아래 행은 한 칸씩 당겨진다.</summary>
public sealed record GSheetDeleteRow(int Row) : GSheetEdit;

/// <summary>셀 값을 쓴다. Value 는 string(문자 그대로, 수식 해석 없음) | double(숫자) | bool. 서식은 건드리지 않는다.</summary>
public sealed record GSheetSetCell(int Row, int Column, object Value) : GSheetEdit;

/// <summary>
/// Column 위치에 빈 열을 끼운다. 기존 Column 이후 열은 한 칸씩 밀린다. InheritFromBefore 면 왼쪽, 아니면 오른쪽 열의 서식을 따른다.
/// 물려받은 서식 중 숫자 표시 형식과 데이터 확인 규칙은 지운다(옆 열의 날짜 형식·드롭다운이 새 열 값의 표시 값을 바꾸지 않도록).
/// </summary>
public sealed record GSheetInsertColumn(int Column, bool InheritFromBefore) : GSheetEdit;

/// <summary>스프레드시트 탭의 sheetId(gid)·이름. 제외 탭('#'·'//')도 포함한다.</summary>
public sealed record GSheetProperties(int SheetId, string Title);

public static class GAuthService
{
    /// <summary>
    /// 구글 드라이브 폴더 내의 모든 구글 시트를 읽어 GSheetTable로 생성합니다.
    /// 하나의 시트에 여러 탭이 존재하는 경우 각각 하나의 테이블이 됩니다.
    /// 테이블의 이름(시트의 탭)이 #이나 //로 시작하는 경우 테이블 생성에서 제외됩니다.
    /// </summary>
    /// <param name="folderLink">구글 드라이브 폴더 경로</param>
    /// <returns>폴더 내에 존재하는 구글 시트 파일이 갖는 모든 테이블</returns>
    /// <summary>
    /// Google 인증을 수행하고(필요하면 브라우저 로그인) 로그인한 계정 이메일을 돌려준다.
    /// </summary>
    public static string Authorize(UserCredentialConfig config, CancellationToken cancellationToken)
        => GAuthAPIHandler.Authorize(config, cancellationToken);

    public static List<GSheetTable> GetTableFromGSheet(string folderLink)
    {
        List<GSheetTable> tables = new();
        
        try
        {
            // 폴더 내의 모든 Google Sheets 파일 가져오기
            var googleSheets = GetAllGoogleSheets(folderLink);
            Logger.Instance.Log($"[GAuthService] Found {googleSheets.Count} Google Sheets");
            
            var sheetsService = GAuthAPIHandler.GetSheetsService();
            
            foreach (var sheetFile in googleSheets)
            {
                try
                {
                    Logger.Instance.Log($"[GAuthService] Processing sheet: {sheetFile.Name}");
                    
                    // 시트의 모든 탭 정보 가져오기
                    var spreadsheet = sheetsService.Spreadsheets.Get(sheetFile.Id).Execute();
                    
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        var sheetTitle = sheet.Properties.Title;
                        
                        // 제외 탭 필터링 (# 또는 //로 시작하는 경우)
                        if (IsExcludedTab(sheetTitle))
                        {
                            Logger.Instance.Log($"[GAuthService] Skipping excluded tab: {sheetTitle}");
                            continue;
                        }
                        
                        Logger.Instance.Log($"[GAuthService] Converting sheet tab to GSheetTable: {sheetTitle}");
                        
                        // 시트 데이터 읽기
                        var range = $"{sheetTitle}!A:Z"; // A부터 Z열까지 모든 행
                        var request = sheetsService.Spreadsheets.Values.Get(sheetFile.Id, range);
                        var response = request.Execute();
                        
                        if (response.Values != null && response.Values.Count > 0)
                        {
                            var gSheetTable = ConvertToGSheetTable(response.Values, $"{sheetFile.Name}_{sheetTitle}", sheetFile.Id, sheetTitle);
                            if (gSheetTable.Data != null && gSheetTable.Data.Count > 0)
                            {
                                tables.Add(gSheetTable);
                                Logger.Instance.Log($"[GAuthService] Successfully created GSheetTable: {gSheetTable.TableName} ({gSheetTable.Data.Count} rows)");
                            }
                        }
                        else
                        {
                            Logger.Instance.Warning($"[GAuthService] No data found in sheet tab: {sheetTitle}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"[GAuthService] Error processing sheet {sheetFile.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[GAuthService] Error in GetTableFromGSheet: {ex.Message}\n{ex.StackTrace}");
        }
        
        Logger.Instance.Log($"[GAuthService] Total GSheetTables created: {tables.Count}");
        return tables;
    }

    /// <summary>
    /// 폴더 내 구글 시트의 탭 목록만 가져온다(셀 데이터는 받지 않는다).
    /// 제외 규칙은 GetTableFromGSheet 와 같다. 오류는 삼키지 않고 호출자에게 던진다.
    /// </summary>
    public static List<GSheetTab> GetTabsFromGSheet(string folderLink)
    {
        var tabs = new List<GSheetTab>();
        var sheetsService = GAuthAPIHandler.GetSheetsService();

        foreach (var sheetFile in GetAllGoogleSheets(folderLink))
        {
            var request = sheetsService.Spreadsheets.Get(sheetFile.Id);
            request.Fields = "sheets.properties(sheetId,title)";
            var spreadsheet = request.Execute();

            foreach (var sheet in spreadsheet.Sheets)
            {
                var sheetTitle = sheet.Properties.Title;
                if (IsExcludedTab(sheetTitle))
                    continue;

                tabs.Add(new GSheetTab
                {
                    SpreadsheetName = sheetFile.Name,
                    SpreadsheetId = sheetFile.Id,
                    SheetTitle = sheetTitle,
                    SheetId = sheet.Properties.SheetId ?? 0,
                    TableName = ToTableName($"{sheetFile.Name}_{sheetTitle}"),
                });
            }
        }

        return tabs;
    }

    /// <summary>
    /// 탭 하나의 데이터만 읽어 GSheetTable 로 만든다. 범위·행 처리 규칙은 GetTableFromGSheet 와 같다.
    /// </summary>
    public static GSheetTable GetTableFromGSheetTab(GSheetTab tab)
    {
        var sheetsService = GAuthAPIHandler.GetSheetsService();
        var response = sheetsService.Spreadsheets.Values.Get(tab.SpreadsheetId, $"{tab.SheetTitle}!A:Z").Execute();
        if (response.Values == null || response.Values.Count == 0)
        {
            return new GSheetTable
            {
                TableName = tab.TableName,
                Data = new List<string[]>(),
                SpreadsheetId = tab.SpreadsheetId,
                SheetTitle = tab.SheetTitle
            };
        }

        return ConvertToGSheetTable(response.Values, tab.TableName, tab.SpreadsheetId, tab.SheetTitle);
    }

    /// <summary>
    /// 탭의 A:Z 값을 행 위치 그대로 읽는다. 빈 행·EOF 이후 행도 지우지 않으므로 인덱스 = 시트 행(0부터)이다.
    /// 범위·값 형식(표시 값)은 GetTableFromGSheetTab 과 같다. 끝의 빈 행은 API 가 돌려주지 않는다.
    /// </summary>
    public static List<string[]> GetGSheetTabValues(GSheetTab tab)
    {
        var sheetsService = GAuthAPIHandler.GetSheetsService();
        var response = sheetsService.Spreadsheets.Values.Get(tab.SpreadsheetId, $"{QuoteSheetTitle(tab.SheetTitle)}!A:Z").Execute();

        return (response.Values ?? new List<IList<object>>())
            .Select(row => row == null
                ? Array.Empty<string>()
                : row.Select(cell => cell?.ToString() ?? string.Empty).ToArray())
            .ToList();
    }

    /// <summary>
    /// 편집을 batchUpdate 한 번으로 적용한다. API 가 요청을 순서대로 적용하고, 하나라도 잘못되면 아무것도 적용하지 않는다.
    /// 셀은 userEnteredValue 만 바꾸므로 서식·데이터 확인 규칙은 남는다.
    /// </summary>
    public static void EditGSheetTab(GSheetTab tab, IReadOnlyList<GSheetEdit> edits)
    {
        if (edits.Count == 0)
            return;

        var requests = edits.SelectMany(edit => ToRequests(tab.SheetId, edit)).ToList();
        var sheetsService = GAuthAPIHandler.GetSheetsService();
        sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = requests }, tab.SpreadsheetId).Execute();

        Logger.Instance.Log($"[GAuthService] EditGSheetTab 완료: {tab.SpreadsheetName}/{tab.SheetTitle} ({requests.Count} request(s))");
    }

    private static IEnumerable<Request> ToRequests(int sheetId, GSheetEdit edit)
    {
        DimensionRange RowRange(int row) => new()
        {
            SheetId = sheetId,
            Dimension = "ROWS",
            StartIndex = row,
            EndIndex = row + 1,
        };

        GridRange ColumnGrid(int column) => new()
        {
            SheetId = sheetId,
            StartColumnIndex = column,
            EndColumnIndex = column + 1,
        };

        switch (edit)
        {
            case GSheetInsertRow insert:
                yield return new Request
                {
                    InsertDimension = new InsertDimensionRequest
                    {
                        Range = RowRange(insert.Row),
                        InheritFromBefore = insert.InheritFromBefore,
                    }
                };
                break;
            case GSheetDeleteRow delete:
                yield return new Request
                {
                    DeleteDimension = new DeleteDimensionRequest { Range = RowRange(delete.Row) }
                };
                break;
            case GSheetSetCell cell:
                yield return new Request
                {
                    UpdateCells = new UpdateCellsRequest
                    {
                        Start = new GridCoordinate { SheetId = sheetId, RowIndex = cell.Row, ColumnIndex = cell.Column },
                        Rows = new List<RowData>
                        {
                            new() { Values = new List<CellData> { new() { UserEnteredValue = ToExtendedValue(cell.Value) } } }
                        },
                        Fields = "userEnteredValue",
                    }
                };
                break;
            case GSheetInsertColumn insert:
                yield return new Request
                {
                    InsertDimension = new InsertDimensionRequest
                    {
                        Range = new DimensionRange
                        {
                            SheetId = sheetId,
                            Dimension = "COLUMNS",
                            StartIndex = insert.Column,
                            EndIndex = insert.Column + 1,
                        },
                        InheritFromBefore = insert.InheritFromBefore,
                    }
                };
                // 숫자 표시 형식을 자동으로 되돌린다. 리더는 표시 값을 읽으므로 옆 열의 날짜·백분율 형식이 남으면 값이 바뀐다.
                yield return new Request
                {
                    RepeatCell = new RepeatCellRequest
                    {
                        Range = ColumnGrid(insert.Column),
                        Cell = new CellData { UserEnteredFormat = new CellFormat() },
                        Fields = "userEnteredFormat.numberFormat",
                    }
                };
                // 규칙 없이 보내면 범위의 데이터 확인 규칙을 지운다
                yield return new Request
                {
                    SetDataValidation = new SetDataValidationRequest { Range = ColumnGrid(insert.Column) }
                };
                break;
            default:
                throw new ArgumentException($"알 수 없는 편집: {edit.GetType().Name}", nameof(edit));
        }
    }

    /// <summary>스프레드시트의 모든 탭(제외 탭 포함)을 순서대로 돌려준다.</summary>
    public static List<GSheetProperties> GetGSheetProperties(string spreadsheetId)
    {
        var request = GAuthAPIHandler.GetSheetsService().Spreadsheets.Get(spreadsheetId);
        request.Fields = "sheets.properties(sheetId,title)";
        return (request.Execute().Sheets ?? new List<Sheet>())
            .Select(s => new GSheetProperties(s.Properties.SheetId ?? 0, s.Properties.Title))
            .ToList();
    }

    /// <summary>
    /// 스프레드시트에 tab.SheetId·tab.SheetTitle 로 탭을 추가하고 rows 를 A1 부터 문자열 값으로 쓴다.
    /// batchUpdate 한 번이라 탭 추가와 값 쓰기는 함께 적용되거나 함께 실패한다.
    /// </summary>
    public static void AddGSheetTab(GSheetTab tab, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var requests = new List<Request>
        {
            new()
            {
                AddSheet = new AddSheetRequest
                {
                    Properties = new SheetProperties { SheetId = tab.SheetId, Title = tab.SheetTitle }
                }
            },
            WriteRows(tab.SheetId, rows),
        };
        GAuthAPIHandler.GetSheetsService().Spreadsheets
            .BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = requests }, tab.SpreadsheetId).Execute();

        Logger.Instance.Log($"[GAuthService] AddGSheetTab 완료: {tab.SpreadsheetName}/{tab.SheetTitle}");
    }

    /// <summary>
    /// 기존 탭(tab.SheetId)의 이름을 tab.SheetTitle 로 바꾸고 rows 를 A1 부터 문자열 값으로 쓴다. 새 파일의 기본 탭을 테이블로 만들 때 쓴다.
    /// </summary>
    public static void InitGSheetTab(GSheetTab tab, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var requests = new List<Request>
        {
            new()
            {
                UpdateSheetProperties = new UpdateSheetPropertiesRequest
                {
                    Properties = new SheetProperties { SheetId = tab.SheetId, Title = tab.SheetTitle },
                    Fields = "title",
                }
            },
            WriteRows(tab.SheetId, rows),
        };
        GAuthAPIHandler.GetSheetsService().Spreadsheets
            .BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = requests }, tab.SpreadsheetId).Execute();

        Logger.Instance.Log($"[GAuthService] InitGSheetTab 완료: {tab.SpreadsheetName}/{tab.SheetTitle}");
    }

    private static Request WriteRows(int sheetId, IReadOnlyList<IReadOnlyList<string>> rows) => new()
    {
        UpdateCells = new UpdateCellsRequest
        {
            Start = new GridCoordinate { SheetId = sheetId, RowIndex = 0, ColumnIndex = 0 },
            Rows = rows
                .Select(row => new RowData
                {
                    Values = row.Select(text => new CellData { UserEnteredValue = new ExtendedValue { StringValue = text } }).ToList()
                })
                .ToList(),
            Fields = "userEnteredValue",
        }
    };

    /// <summary>
    /// 폴더에 빈 구글 시트 파일을 만들고 (파일 ID, 링크)를 돌려준다. 공유 드라이브 폴더도 된다.
    /// 드라이브 쓰기 범위(drive)가 필요하다. 예전 읽기 전용 범위로 받은 토큰이면 403(insufficient authentication scopes)이다.
    /// </summary>
    public static (string Id, string Link) CreateGoogleSheet(string folderLink, string name)
    {
        var request = GAuthAPIHandler.GetDriveService().Files.Create(new Google.Apis.Drive.v3.Data.File
        {
            Name = name,
            MimeType = "application/vnd.google-apps.spreadsheet",
            Parents = new List<string> { ExtractFolderId(folderLink) },
        });
        request.SupportsAllDrives = true;
        request.Fields = "id, webViewLink";
        var file = request.Execute();

        Logger.Instance.Log($"[GAuthService] CreateGoogleSheet 완료: {name} (ID: {file.Id})");
        return (file.Id, file.WebViewLink ?? $"https://docs.google.com/spreadsheets/d/{file.Id}");
    }

    /// <summary>파일을 휴지통으로 옮긴다(드라이브에서 되돌릴 수 있다). 휴지통 파일은 폴더 목록에서 빠진다.</summary>
    public static void TrashFile(string fileId)
    {
        var request = GAuthAPIHandler.GetDriveService().Files.Update(new Google.Apis.Drive.v3.Data.File { Trashed = true }, fileId);
        request.SupportsAllDrives = true;
        request.Execute();

        Logger.Instance.Log($"[GAuthService] TrashFile 완료: {fileId}");
    }

    private static ExtendedValue ToExtendedValue(object value) => value switch
    {
        double number => new ExtendedValue { NumberValue = number },
        bool flag => new ExtendedValue { BoolValue = flag },
        string text => new ExtendedValue { StringValue = text },
        _ => throw new ArgumentException($"셀 값은 string | double | bool 이어야 합니다: {value.GetType().Name}", nameof(value))
    };

    /// <summary>A1 표기의 시트 이름. 공백·특수 문자가 있어도 되도록 항상 작은따옴표로 감싼다.</summary>
    private static string QuoteSheetTitle(string sheetTitle) => $"'{sheetTitle.Replace("'", "''")}'";

    /// <summary>
    /// GetTableFromGSheet 의 테이블 이름("{시트파일}_{탭}")을 추출 결과 .bin 이름으로 바꾼다.
    /// 첫 '_' 앞 토막과 '_' 를 지운다. BytesExtractorFromGSheet 가 .bin 이름을 정할 때 쓰는 규칙이다.
    /// </summary>
    public static string ToTableName(string gsheetTableName)
    {
        var head = gsheetTableName.Split('_')[0];
        return gsheetTableName.Replace(head + "_", "");
    }

    /// <summary>
    /// 캐시된 OAuth 토큰이 있는지 확인한다. 없으면 GSheet 호출이 브라우저 로그인을 기다리며 멈춘다.
    /// </summary>
    public static bool HasCachedToken() => GAuthAPIHandler.HasCachedToken();

    private static bool IsExcludedTab(string sheetTitle)
        => sheetTitle.StartsWith("#") || sheetTitle.StartsWith("//");

    /// <summary>
    /// Google Sheets의 값들을 GSheetTable로 변환합니다.
    /// </summary>
    /// <param name="values">Google Sheets에서 읽어온 값들</param>
    /// <param name="tableName">생성할 테이블의 이름</param>
    /// <returns>변환된 GSheetTable</returns>
    private static GSheetTable ConvertToGSheetTable(
        IList<IList<object>> values,
        string tableName,
        string spreadsheetId,
        string sheetTitle)
    {
        var table = new GSheetTable
        {
            TableName = tableName,
            Data = new List<string[]>(),
            SpreadsheetId = spreadsheetId,
            SheetTitle = sheetTitle
        };
        
        if (values == null || values.Count == 0)
        {
            Logger.Instance.Warning($"[GAuthService] No values to convert for table: {tableName}");
            return table;
        }
        
        try
        {
            // 모든 행을 string[]로 변환
            foreach (var row in values)
            {
                // 빈 행 체크
                if (row.All(cell => string.IsNullOrWhiteSpace(cell?.ToString())))
                {
                    continue;
                }
                
                // EOF 마커 확인
                if (row.Count > 0 && row[0]?.ToString()?.Equals("EOF", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Logger.Instance.Log($"[GAuthService] Reached EOF marker in {tableName}");
                    break;
                }
                
                var rowData = new string[row.Count];
                for (int i = 0; i < row.Count; i++)
                {
                    rowData[i] = row[i]?.ToString() ?? string.Empty;
                }
                
                table.Data.Add(rowData);
            }
            
            Logger.Instance.Log($"[GAuthService] GSheetTable created: {tableName} with {table.Data.Count} rows");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error($"[GAuthService] Error converting to GSheetTable: {ex.Message}\n{ex.StackTrace}");
        }
        
        return table;
    }

    /// <summary>
    /// GSheet의 지정 탭을 rows로 덮어씁니다. clear → update 순서로 완전 갱신합니다.
    /// </summary>
    public static void UpdateGSheetTable(string spreadsheetId, string sheetTitle, List<string[]> rows)
    {
        var sheetsService = GAuthAPIHandler.GetSheetsService();
        var clearRange = $"{sheetTitle}!A:Z";

        try
        {
            var clearRequest = sheetsService.Spreadsheets.Values.Clear(
                new ClearValuesRequest(), spreadsheetId, clearRange);
            clearRequest.Execute();
        }
        catch (Exception ex)
        {
            throw new Exception($"GSheet 클리어 실패 (데이터 유지됨): {ex.Message}", ex);
        }

        var ilist = rows
            .Select(r => (IList<object>)r.Select(c => (object)c).ToList())
            .ToList<IList<object>>();
        var body = new ValueRange { Values = ilist };
        var updateRequest = sheetsService.Spreadsheets.Values.Update(
            body, spreadsheetId, $"{sheetTitle}!A1");
        updateRequest.ValueInputOption =
            SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        try
        {
            updateRequest.Execute();
        }
        catch (Exception ex)
        {
            throw new Exception($"GSheet 업데이트 실패 (GSheet가 비어 있습니다 — 재저장하세요): {ex.Message}", ex);
        }

        Logger.Instance.Log($"[GAuthService] UpdateGSheetTable 완료: {spreadsheetId} [{sheetTitle}] ({rows.Count} rows)");
    }

    /// <summary>
    /// 구글 드라이브에 존재하는 파일을 읽어 리스트로 반환합니다.
    /// </summary>
    /// <param name="folderLink">검색할 폴더를 지정합니다.</param>
    /// <param name="searchPatterns">검색할 파일명 패턴 배열. null인 경우 폴더내 모든 파일을 읽어옵니다.</param>
    /// <returns></returns>
    public static List<Google.Apis.Drive.v3.Data.File> GetAllFiles(string folderLink, string[] searchPatterns = null)
    {
        var service = GAuthAPIHandler.GetDriveService();
        // 폴더 링크에서 폴더 ID 추출
        string folderId = ExtractFolderId(folderLink);
        Logger.Instance.Log($"[GAuthService] Get Files from : {folderLink} -> {folderId}");

        // 기본 쿼리: 폴더 내의 모든 파일 (휴지통 제외)
        string baseQuery = $"'{folderId}' in parents and trashed=false";

        // searchPatterns가 지정된 경우 파일명 필터 추가
        if (searchPatterns != null && searchPatterns.Length > 0)
        {
            var nameConditions = new List<string>();
            foreach (var pattern in searchPatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    // 와일드카드 처리: *를 포함한 패턴을 contains로 변환
                    if (pattern.Contains("*"))
                    {
                        // 예: "Player*" -> "name contains 'Player'"
                        var searchTerm = pattern.Replace("*", "").Trim();
                        if (!string.IsNullOrEmpty(searchTerm))
                        {
                            nameConditions.Add($"name contains '{searchTerm}'");
                        }
                    }
                    else
                    {
                        // 정확한 이름 매칭
                        nameConditions.Add($"name = '{pattern}'");
                    }
                }
            }

            if (nameConditions.Count > 0)
            {
                // OR 조건으로 결합
                string nameQuery = "(" + string.Join(" or ", nameConditions) + ")";
                baseQuery += " and " + nameQuery;
            }
        }

        var request = service.Files.List();
        request.Q = baseQuery;
        request.Fields = "files(id, name)";

        Logger.Instance.Log($"[GAuthService] Query: {baseQuery}");
        Logger.Instance.Log($"[GAuthService] Req FindFiles from {folderLink}");

        var result = request.Execute();
        Logger.Instance.Log($"[GAuthService] Got File List (count: {result.Files.Count})");

        // 결과 로깅
        foreach (var file in result.Files)
        {
            Logger.Instance.Log($"[GAuthService] Found file: {file.Name} (ID: {file.Id})");
        }

        return result.Files.ToList();
    }

    public static IList<Google.Apis.Drive.v3.Data.File> GetAllGoogleSheets(string folderLink)
    {
        var service = GAuthAPIHandler.GetDriveService();

        // 폴더 링크에서 폴더 ID 추출
        string folderId = ExtractFolderId(folderLink);
        Logger.Instance.Log($"[GAuthService] Get Google Sheets from : {folderLink} -> {folderId}");

        // 폴더 내의 Google Sheets 파일만 검색하는 올바른 쿼리
        var request = service.Files.List();
        request.Q = $"'{folderId}' in parents and mimeType='application/vnd.google-apps.spreadsheet' and trashed=false";
        request.Fields = "files(id, name)";
        Logger.Instance.Log($"[GAuthService] Req FindGoogleSheet from {folderLink}");

        var result = request.Execute();
        Console.WriteLine($"[GAuthService] Got Google Sheet List (count: {result.Files.Count})");
        return result.Files;
    }
    
    /// <summary>
    /// Google Drive 링크에서 폴더 ID 추출
    /// </summary>
    private static string ExtractFolderId(string folderIdOrLink)
    {
        // 이미 ID만 있는 경우
        if (!folderIdOrLink.Contains("/"))
            return folderIdOrLink;
            
        // 링크에서 ID 추출
        // 예: https://drive.google.com/drive/folders/FOLDER_ID
        var parts = folderIdOrLink.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "folders" && i + 1 < parts.Length)
            {
                return parts[i + 1].Split('?')[0]; // 쿼리 파라미터 제거
            }
        }
        
        // ID 추출 실패시 원본 반환
        return folderIdOrLink;
    }
}

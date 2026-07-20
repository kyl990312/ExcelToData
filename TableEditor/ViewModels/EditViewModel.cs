using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using ClosedXML.Excel;
using GAuth;
using TableEditor.Core;
using TableEditor.Services;

namespace TableEditor.ViewModels;

/// <summary>
/// 편집 탭(뷰어 통합): 좌측 테이블 리스트+검색, 우측 데이터 그리드.
/// xlsx는 조회+편집, GSheet는 1차 읽기 전용(기획서 D2).
/// </summary>
public class EditViewModel : ViewModelBase
{
    private readonly ConfigService _cfg;
    private readonly PipelineService _pipe;
    private readonly Action<string> _status;

    private List<string> _all = new();
    private List<GSheetTable> _gsheetCache = new();

    private bool _isGSheet;
    private string _searchText = string.Empty;
    private string? _selectedTable;
    private string? _selectedSheet;
    private bool _suppressSheetLoad;
    private DataView? _tableView;
    private string _typesSummary = string.Empty;
    private string _changeInfo = "변경 없음";
    private bool _newPanelOpen;
    private string _newTableName = string.Empty;
    private string _newColumnsText = "Id:int\nName:string";

    private DataTable? _dt;
    private string? _loadedFile;
    private string[] _colNames = Array.Empty<string>();
    private string[] _colTypes = Array.Empty<string>();

    public EditViewModel(ConfigService cfg, PipelineService pipe, Action<string> status)
    {
        _cfg = cfg;
        _pipe = pipe;
        _status = status;

        SearchCommand = new RelayCommand(ApplySearch);
        RefreshCommand = new RelayCommand(LoadTables);
        SaveCommand = new RelayCommand(Save, () => _dt != null && !_pipe.IsBusy);
        RevertCommand = new RelayCommand(Revert, () => HasChanges);
        SaveExtractCommand = new RelayCommand(SaveAndExtract,
            () => !_isGSheet && _dt != null && _selectedTable != null && !_pipe.IsBusy);
        OpenNewTableCommand = new RelayCommand(() => NewPanelOpen = true, () => !_isGSheet);
        CancelNewTableCommand = new RelayCommand(() => NewPanelOpen = false);
        CreateTableCommand = new RelayCommand(CreateTable);

        LoadTables();
    }

    // --- 바인딩 속성 ---

    public ObservableCollection<string> Tables { get; } = new();
    public ObservableCollection<string> SheetNames { get; } = new();

    public bool IsGSheet
    {
        get => _isGSheet;
        set
        {
            if (Set(ref _isGSheet, value))
            {
                OnPropertyChanged(nameof(IsLocal));
                LoadTables();
            }
        }
    }

    public bool IsLocal
    {
        get => !_isGSheet;
        set => IsGSheet = !value;
    }

    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    public string? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (Set(ref _selectedTable, value))
                LoadSelected();
        }
    }

    public string? SelectedSheet
    {
        get => _selectedSheet;
        set
        {
            if (Set(ref _selectedSheet, value) && !_suppressSheetLoad && value != null)
                LoadSheet(value);
        }
    }

    public DataView? TableView
    {
        get => _tableView;
        private set => Set(ref _tableView, value);
    }

    public string TypesSummary
    {
        get => _typesSummary;
        private set => Set(ref _typesSummary, value);
    }

    public string ChangeInfo
    {
        get => _changeInfo;
        private set => Set(ref _changeInfo, value);
    }

    public bool NewPanelOpen
    {
        get => _newPanelOpen;
        set => Set(ref _newPanelOpen, value);
    }

    public string NewTableName
    {
        get => _newTableName;
        set => Set(ref _newTableName, value);
    }

    public string NewColumnsText
    {
        get => _newColumnsText;
        set => Set(ref _newColumnsText, value);
    }

    public bool HasChanges => _dt?.GetChanges() != null;

    public RelayCommand SearchCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand SaveExtractCommand { get; }
    public RelayCommand OpenNewTableCommand { get; }
    public RelayCommand CancelNewTableCommand { get; }
    public RelayCommand CreateTableCommand { get; }

    // --- 테이블 리스트 / 검색 ---

    private string ResolveSrcDir()
    {
        var src = string.IsNullOrWhiteSpace(_cfg.Binary.SrcDirectory) ? "Data" : _cfg.Binary.SrcDirectory;
        return Path.GetFullPath(src);
    }

    public void LoadTables()
    {
        _all.Clear();
        SelectedTable = null;
        ClearGrid();

        if (!_isGSheet)
        {
            var src = ResolveSrcDir();
            if (!Directory.Exists(src))
            {
                _status($"원본 폴더가 없습니다: {src} (설정 탭에서 경로를 지정하세요)");
                ApplySearch();
                return;
            }

            _all = ExcelToData.ProjectDefine.SearchPatterns
                .SelectMany(p => Directory.GetFiles(src, p))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Cast<string>()
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ApplySearch();
            _status($"테이블 {_all.Count}개 로드됨 ({src})");
        }
        else
        {
            FetchGSheetAsync();
        }
    }

    private async void FetchGSheetAsync()
    {
        var link = _cfg.Binary.SrcGDriveLink;
        if (string.IsNullOrWhiteSpace(link))
        {
            _status("GSheet 링크가 비어 있습니다. 설정 탭에서 GDrive 링크를 지정하세요.");
            ApplySearch();
            return;
        }

        _status("GSheet 목록을 불러오는 중…");
        try
        {
            var tables = await Task.Run(() => GAuthService.GetTableFromGSheet(link));
            _gsheetCache = tables;
            _all = tables
                .Select(t => NormalizeName(t.TableName))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            ApplySearch();
            _status($"GSheet 테이블 {_all.Count}개 로드됨");
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] GSheet 로드 실패: {ex}");
            _status($"GSheet 로드 실패: {ex.Message}");
            ApplySearch();
        }
    }

    /// <summary>탭명 prefix(시트명_) 제거 — 엔진의 -each 매칭 규약과 동일</summary>
    private static string NormalizeName(string name)
    {
        var i = name.IndexOf('_');
        return i >= 0 ? name[(i + 1)..] : name;
    }

    /// <summary>검색 버튼: 검색어 포함 테이블만 표시, 비어 있으면 전체 표시</summary>
    private void ApplySearch()
    {
        var keyword = _searchText.Trim();
        var filtered = string.IsNullOrEmpty(keyword)
            ? _all
            : _all.Where(n => n.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        var keep = _selectedTable;
        Tables.Clear();
        foreach (var name in filtered)
            Tables.Add(name);

        if (keep != null && filtered.Contains(keep))
            _selectedTable = keep; // 선택 유지 (재로드 방지)
    }

    // --- 그리드 로드 ---

    private void ClearGrid()
    {
        _dt = null;
        _loadedFile = null;
        _colNames = Array.Empty<string>();
        _colTypes = Array.Empty<string>();
        TableView = null;
        TypesSummary = string.Empty;
        ChangeInfo = "변경 없음";
        _suppressSheetLoad = true;
        SheetNames.Clear();
        SelectedSheet = null;
        _suppressSheetLoad = false;
    }

    private void LoadSelected()
    {
        ClearGrid();
        if (_selectedTable == null)
            return;

        try
        {
            if (_isGSheet)
                LoadFromGSheetCache(_selectedTable);
            else
                LoadFromXlsx(_selectedTable);
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] 테이블 로드 실패({_selectedTable}): {ex}");
            _status($"테이블 로드 실패: {ex.Message}");
        }
    }

    private void LoadFromXlsx(string tableName)
    {
        var src = ResolveSrcDir();
        _loadedFile = ExcelToData.ProjectDefine.SearchPatterns
            .Select(p => Path.Combine(src, tableName + p.TrimStart('*')))
            .FirstOrDefault(File.Exists);

        if (_loadedFile == null)
        {
            _status($"파일을 찾을 수 없습니다: {tableName}");
            return;
        }

        using var wb = new XLWorkbook(_loadedFile);
        _suppressSheetLoad = true;
        SheetNames.Clear();
        foreach (var ws in wb.Worksheets)
            SheetNames.Add(ws.Name);
        _suppressSheetLoad = false;

        if (SheetNames.Count > 0)
            SelectedSheet = SheetNames[0]; // setter가 LoadSheet 수행
    }

    private void LoadSheet(string sheetName)
    {
        if (_loadedFile == null)
            return;

        using var wb = new XLWorkbook(_loadedFile);
        var ws = wb.Worksheet(sheetName);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow < 2 || lastCol < 1)
        {
            _status($"헤더 2행(컬럼명·타입) 규약에 맞지 않는 시트입니다: {sheetName}");
            return;
        }

        var names = new string[lastCol];
        var types = new string[lastCol];
        for (int c = 1; c <= lastCol; c++)
        {
            names[c - 1] = ws.Cell(1, c).GetString();
            types[c - 1] = ws.Cell(2, c).GetString();
        }

        var rows = new List<string[]>();
        for (int r = 3; r <= lastRow; r++)
        {
            var row = new string[lastCol];
            for (int c = 1; c <= lastCol; c++)
                row[c - 1] = ws.Cell(r, c).GetString();
            rows.Add(row);
        }

        BuildDataTable(names, types, rows);
    }

    private void LoadFromGSheetCache(string tableName)
    {
        var found = _gsheetCache.FirstOrDefault(t => NormalizeName(t.TableName) == tableName);
        if (found.Data == null || found.Data.Count < 2)
        {
            _status($"GSheet 테이블 데이터가 비어 있습니다: {tableName}");
            return;
        }

        var names = found.Data[0];
        var types = found.Data[1];
        var rows = found.Data.Skip(2).ToList();
        BuildDataTable(names, types, rows);
    }

    private void BuildDataTable(string[] names, string[] types, List<string[]> rows)
    {
        _colNames = names;
        _colTypes = types;

        var dt = new DataTable();
        for (int i = 0; i < names.Length; i++)
        {
            var colName = string.IsNullOrWhiteSpace(names[i]) ? $"Col{i + 1}" : names[i];
            // DataTable 컬럼명 중복 방지
            var unique = colName;
            int suffix = 2;
            while (dt.Columns.Contains(unique))
                unique = $"{colName}_{suffix++}";
            dt.Columns.Add(unique, typeof(string));
        }

        foreach (var row in rows)
        {
            var dr = dt.NewRow();
            for (int i = 0; i < names.Length; i++)
                dr[i] = i < row.Length ? row[i] : string.Empty;
            dt.Rows.Add(dr);
        }

        dt.AcceptChanges();
        dt.RowChanged += (_, _) => UpdateChangeInfo();
        dt.RowDeleted += (_, _) => UpdateChangeInfo();

        _dt = dt;
        TableView = dt.DefaultView;
        TypesSummary = string.Join("   ", names.Zip(types, (n, t) => $"{n}:{t}"));
        UpdateChangeInfo();
    }

    private void UpdateChangeInfo()
    {
        var changes = _dt?.GetChanges();
        ChangeInfo = changes == null ? "변경 없음" : $"변경 {changes.Rows.Count}건 · 미저장";
        OnPropertyChanged(nameof(HasChanges));
    }

    // --- 저장 / 되돌리기 / 신규 테이블 ---

    private void Save()
    {
        if (_dt == null) return;
        if (_isGSheet)
        {
            _ = SaveToGSheetAsync();
            return;
        }
        if (_loadedFile == null || _selectedSheet == null) return;

        try
        {
            using var wb = new XLWorkbook(_loadedFile);
            var ws = wb.Worksheet(_selectedSheet);

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = Math.Max(ws.LastColumnUsed()?.ColumnNumber() ?? 0, _colNames.Length);
            if (lastRow > 0)
                ws.Range(1, 1, lastRow, lastCol).Clear(XLClearOptions.Contents);

            for (int c = 0; c < _colNames.Length; c++)
            {
                ws.Cell(1, c + 1).Value = _colNames[c];
                ws.Cell(2, c + 1).Value = _colTypes[c];
            }

            int r = 3;
            foreach (DataRow row in _dt.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;
                for (int c = 0; c < _colNames.Length; c++)
                    WriteCell(ws.Cell(r, c + 1), row[c]?.ToString() ?? string.Empty, _colTypes[c]);
                r++;
            }

            wb.Save();
            _dt.AcceptChanges();
            UpdateChangeInfo();
            _status($"저장 완료: {Path.GetFileName(_loadedFile)} [{_selectedSheet}]");
            UiLog.Write($"[TableEditor] 저장: {_loadedFile} [{_selectedSheet}]");
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] 저장 실패: {ex}");
            _status($"저장 실패: {ex.Message}");
        }
    }

    private async Task SaveToGSheetAsync()
    {
        var src = _gsheetCache.FirstOrDefault(t => NormalizeName(t.TableName) == _selectedTable);
        if (src.SpreadsheetId == null)
        {
            _status("GSheet 원본 참조를 찾을 수 없습니다.");
            return;
        }

        var rows = new List<string[]>();
        rows.Add(_colNames);
        rows.Add(_colTypes);
        foreach (DataRow dr in _dt!.Rows)
        {
            if (dr.RowState == DataRowState.Deleted) continue;
            var row = new string[_colNames.Length];
            for (int i = 0; i < _colNames.Length; i++)
                row[i] = dr[i]?.ToString() ?? string.Empty;
            rows.Add(row);
        }

        _status($"GSheet 되쓰는 중… ({src.SheetTitle})");
        try
        {
            await Task.Run(() => GAuthService.UpdateGSheetTable(src.SpreadsheetId, src.SheetTitle, rows));
            _dt.AcceptChanges();
            UpdateChangeInfo();
            _status($"GSheet 저장 완료: {src.SheetTitle}");
            UiLog.Write($"[TableEditor] GSheet 저장: {src.SpreadsheetId} [{src.SheetTitle}]");
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] GSheet 저장 실패: {ex}");
            _status($"GSheet 저장 실패: {ex.Message}");
        }
    }

    /// <summary>타입 힌트에 맞춰 셀 타입 유지(숫자/불리언), 그 외 문자열</summary>
    private static void WriteCell(IXLCell cell, string text, string typeHint)
    {
        var t = typeHint.ToLowerInvariant();
        if ((t.Contains("int") || t.Contains("float") || t.Contains("double") || t.Contains("long"))
            && double.TryParse(text, out var num))
        {
            cell.Value = num;
        }
        else if (t.Contains("bool") && bool.TryParse(text, out var b))
        {
            cell.Value = b;
        }
        else
        {
            cell.Value = text;
        }
    }

    private void Revert()
    {
        _dt?.RejectChanges();
        UpdateChangeInfo();
        _status("변경 사항을 되돌렸습니다.");
    }

    private async void SaveAndExtract()
    {
        var table = _selectedTable;
        if (table == null)
            return;
        Save();
        await _pipe.ExtractEach(new[] { table });
    }

    private void CreateTable()
    {
        var name = _newTableName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _status("테이블명을 입력하세요.");
            return;
        }

        var columns = _newColumnsText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var idx = line.IndexOf(':');
                return idx > 0
                    ? (Name: line[..idx].Trim(), Type: line[(idx + 1)..].Trim())
                    : (Name: line.Trim(), Type: "string");
            })
            .Where(c => c.Name.Length > 0)
            .ToList();

        if (columns.Count == 0)
        {
            _status("컬럼 정의를 한 줄에 하나씩 '이름:타입' 형식으로 입력하세요.");
            return;
        }

        var src = ResolveSrcDir();
        Directory.CreateDirectory(src);
        var path = Path.Combine(src, name + ".xlsx");
        if (File.Exists(path))
        {
            _status($"이미 존재하는 테이블입니다: {name}");
            return;
        }

        try
        {
            using var wb = new XLWorkbook();
            var sheetName = name.Length > 31 ? name[..31] : name;
            var ws = wb.AddWorksheet(sheetName);
            for (int c = 0; c < columns.Count; c++)
            {
                ws.Cell(1, c + 1).Value = columns[c].Name;
                ws.Cell(2, c + 1).Value = columns[c].Type;
            }
            wb.SaveAs(path);

            NewPanelOpen = false;
            _status($"신규 테이블 생성: {name}.xlsx (컬럼 {columns.Count}개)");
            UiLog.Write($"[TableEditor] 신규 테이블 생성: {path}");

            LoadTables();
            SelectedTable = Tables.FirstOrDefault(t => t == name);
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] 신규 테이블 생성 실패: {ex}");
            _status($"신규 테이블 생성 실패: {ex.Message}");
        }
    }
}

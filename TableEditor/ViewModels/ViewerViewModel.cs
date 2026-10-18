using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using TableEditor.Core;
using TableEditor.Services;

namespace TableEditor.ViewModels;

public class ViewerViewModel : ViewModelBase
{
    private readonly ConfigService _cfg;
    private readonly Action<string> _status;

    private List<string> _all = new();
    private string _searchText = string.Empty;
    private string? _selectedTable;
    private DataView? _tableView;
    private string _typesSummary = string.Empty;
    private string _rowInfo = string.Empty;

    private DataTable? _dt;

    public ViewerViewModel(ConfigService cfg, Action<string> status)
    {
        _cfg = cfg;
        _status = status;
        SearchCommand = new RelayCommand(ApplySearch);
        RefreshCommand = new RelayCommand(LoadTables);
        LoadTables();
    }

    public ObservableCollection<string> Tables { get; } = new();

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

    public string RowInfo
    {
        get => _rowInfo;
        private set => Set(ref _rowInfo, value);
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand RefreshCommand { get; }

    private string ResolveDstDir()
    {
        var dst = string.IsNullOrWhiteSpace(_cfg.Binary.DstDirectory) ? "Output" : _cfg.Binary.DstDirectory;
        return Path.GetFullPath(dst);
    }

    public void LoadTables()
    {
        _all.Clear();
        SelectedTable = null;
        ClearGrid();

        var dst = ResolveDstDir();
        if (!Directory.Exists(dst))
        {
            _status($"출력 폴더가 없습니다: {dst} (설정 탭에서 출력 경로를 지정하세요)");
            ApplySearch();
            return;
        }

        _all = Directory.GetFiles(dst, "*.bin")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();

        ApplySearch();
        _status($"바이너리 테이블 {_all.Count}개 로드됨 ({dst})");
    }

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
            _selectedTable = keep;
    }

    private void LoadSelected()
    {
        ClearGrid();
        if (_selectedTable == null) return;

        var dst = ResolveDstDir();
        var binPath = Path.Combine(dst, _selectedTable + ".bin");
        if (!File.Exists(binPath))
        {
            _status($"파일을 찾을 수 없습니다: {binPath}");
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(binPath);
            var schema = BinaryParser.BinarySchema.ReadFromBytes(bytes);
            var rows = new BinaryParser.BinaryReader().ReadFromFile(binPath);
            BuildDataTable(schema, rows);
            _status($"{_selectedTable} 로드 완료");
        }
        catch (Exception ex)
        {
            ClearGrid();
            UiLog.Write($"[TableEditor][Error] .bin 파싱 실패({_selectedTable}): {ex}");
            _status($".bin 파싱 실패: {ex.Message}");
        }
    }

    private void BuildDataTable(BinaryParser.BinarySchema schema, List<Dictionary<string, object>> rows)
    {
        var colNames = schema.Columns.Keys.ToArray();
        var colTypes = schema.Columns.Values.ToArray();

        var dt = new DataTable();
        foreach (var name in colNames)
        {
            var unique = name;
            int suffix = 2;
            while (dt.Columns.Contains(unique)) unique = $"{name}_{suffix++}";
            dt.Columns.Add(unique, typeof(string));
        }

        foreach (var rowDict in rows)
        {
            var dr = dt.NewRow();
            for (int i = 0; i < colNames.Length; i++)
            {
                var val = rowDict.TryGetValue(colNames[i], out var v) ? v : null;
                dr[i] = ValueToString(val);
            }
            dt.Rows.Add(dr);
        }

        dt.AcceptChanges();
        _dt = dt;
        TableView = dt.DefaultView;
        TypesSummary = string.Join("   ", colNames.Zip(colTypes, (n, t) => $"{n}:{t}"));
        RowInfo = $"행 {dt.Rows.Count}개 · 컬럼 {colNames.Length}개";
    }

    private static string ValueToString(object? value)
    {
        if (value == null) return string.Empty;
        if (value is Array arr)
        {
            var parts = new List<string>(arr.Length);
            foreach (var el in arr) parts.Add(el?.ToString() ?? string.Empty);
            return string.Join("|", parts);
        }
        return value.ToString() ?? string.Empty;
    }

    private void ClearGrid()
    {
        _dt = null;
        TableView = null;
        TypesSummary = string.Empty;
        RowInfo = string.Empty;
    }
}

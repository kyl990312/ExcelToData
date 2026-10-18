using System.Collections.ObjectModel;
using System.IO;
using TableAutomationTool.GAuth;
using TableEditor.Core;
using TableEditor.Services;

namespace TableEditor.ViewModels;

/// <summary>
/// 데이터 추출 탭: 원본(xlsx/GSheet) → .bin, 전체/선택(-each) 실행.
/// </summary>
public class ExtractViewModel : ViewModelBase
{
    private readonly ConfigService _cfg;
    private readonly PipelineService _pipe;
    private readonly Action<string> _status;

    private bool _isGSheetSource;
    private bool _checkAll;
    private bool _diffOnly;

    public ExtractViewModel(ConfigService cfg, PipelineService pipe, Action<string> status)
    {
        _cfg = cfg;
        _pipe = pipe;
        _status = status;

        RefreshCommand = new RelayCommand(Refresh, () => !_pipe.IsBusy);
        ExtractAllCommand = new RelayCommand(
            async () => await _pipe.ExtractAll(), () => !_pipe.IsBusy);
        ExtractSelectedCommand = new RelayCommand(
            async () => await ExtractSelected(),
            () => !_pipe.IsBusy && Items.Any(i => i.IsChecked));

        Refresh();
    }

    public ObservableCollection<SelectableItem> Items { get; } = new();

    public bool IsGSheetSource
    {
        get => _isGSheetSource;
        set
        {
            if (Set(ref _isGSheetSource, value))
            {
                OnPropertyChanged(nameof(IsLocalSource));
                Refresh();
            }
        }
    }

    public bool IsLocalSource
    {
        get => !_isGSheetSource;
        set => IsGSheetSource = !value;
    }

    /// <summary>
    /// git 으로 변경이 감지된 워크북의 테이블만 보여준다.
    /// diff 는 파일 단위라 워크북이 바뀌면 그 안의 테이블이 모두 후보가 된다.
    /// </summary>
    public bool DiffOnly
    {
        get => _diffOnly;
        set
        {
            if (Set(ref _diffOnly, value))
                Refresh();
        }
    }

    public bool CheckAll
    {
        get => _checkAll;
        set
        {
            if (Set(ref _checkAll, value))
            {
                foreach (var item in Items)
                    item.IsChecked = value;
            }
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ExtractAllCommand { get; }
    public RelayCommand ExtractSelectedCommand { get; }

    public void Refresh()
    {
        Items.Clear();

        if (!_isGSheetSource)
        {
            var src = string.IsNullOrWhiteSpace(_cfg.Binary.SrcDirectory) ? "Data" : _cfg.Binary.SrcDirectory;
            src = Path.GetFullPath(src);
            if (!Directory.Exists(src))
            {
                _status($"원본 폴더가 없습니다: {src}");
                return;
            }

            // 테이블의 단위는 워크북이 아니라 워크시트다 (.bin 이 워크시트 이름으로 저장된다).
            // 워크북 하나에 테이블이 여러 개 들어 있으므로 시트를 펼쳐서 보여준다.
            var workbooks = TableAutomationTool.ExcelToData.ProjectDefine.SearchPatterns
                .SelectMany(p => Directory.GetFiles(src, p))
                .Distinct()
                .ToList();

            if (_diffOnly)
            {
                var changed = GetChangedWorkbooks(src);
                if (changed == null)
                    return; // 실패 사유는 GetChangedWorkbooks 가 상태바에 남긴다
                workbooks = workbooks
                    .Where(w => changed.Contains(Path.GetFullPath(w)))
                    .ToList();
            }

            var names = workbooks
                .SelectMany(TableAutomationTool.ExcelParser.TableHelper.GetTableNames)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n);
            foreach (var name in names)
                Items.Add(new SelectableItem(name));
            _status(_diffOnly
                ? $"변경된 테이블 {Items.Count}개 (워크북 {workbooks.Count}개 · git diff 기준)"
                : $"추출 대상 {Items.Count}개 테이블 (로컬 · 워크북 {workbooks.Count}개)");
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
            _status("GSheet 링크가 비어 있습니다. 설정 탭에서 지정하세요.");
            return;
        }

        _status("GSheet 목록을 불러오는 중…");
        try
        {
            var tables = await Task.Run(() => GAuthService.GetTableFromGSheet(link));
            var names = tables
                .Select(t =>
                {
                    var i = t.TableName.IndexOf('_');
                    return i >= 0 ? t.TableName[(i + 1)..] : t.TableName;
                })
                .Distinct()
                .OrderBy(n => n);
            Items.Clear();
            foreach (var name in names)
                Items.Add(new SelectableItem(name));
            _status($"추출 대상 {Items.Count}개 (GSheet · 개별 선택 시에도 다운로드는 전체)");
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] GSheet 목록 로드 실패: {ex}");
            _status($"GSheet 목록 로드 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// git 으로 변경이 감지된 워크북의 전체 경로 집합. 실패하면 null 을 돌려주고 사유를 상태바에 남긴다.
    /// 경로 우선순위는 CLI 와 같다: GitConfig.RepositoryPath > 데이터 추출 Src 폴더.
    /// </summary>
    private HashSet<string>? GetChangedWorkbooks(string srcFallback)
    {
        var folder = string.IsNullOrWhiteSpace(_cfg.Git.RepositoryPath) ? srcFallback : _cfg.Git.RepositoryPath;

        try
        {
            var handler = new TableAutomationTool.Vcs.GitHandler(_cfg.Git);
            return handler.GetDiffList(folder)
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (TableAutomationTool.Vcs.VcsException ex)
        {
            UiLog.Write($"[TableEditor][Error] diff 조회 실패: {ex.Message}");
            _status($"Diff 조회 실패: {ex.Message} (설정 탭의 Git 저장소 경로를 확인하세요)");
            return null;
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] diff 조회 실패: {ex}");
            _status($"Diff 조회 실패: {ex.Message}");
            return null;
        }
    }

    private Task ExtractSelected()
    {
        var names = Items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
        return _pipe.ExtractEach(names);
    }
}

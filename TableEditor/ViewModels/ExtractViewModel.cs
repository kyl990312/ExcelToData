using System.Collections.ObjectModel;
using System.IO;
using GAuth;
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

            var names = ExcelToData.ProjectDefine.SearchPatterns
                .SelectMany(p => Directory.GetFiles(src, p))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Cast<string>()
                .Distinct()
                .OrderBy(n => n);
            foreach (var name in names)
                Items.Add(new SelectableItem(name));
            _status($"추출 대상 {Items.Count}개 (로컬)");
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

    private Task ExtractSelected()
    {
        var names = Items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
        return _pipe.ExtractEach(names);
    }
}

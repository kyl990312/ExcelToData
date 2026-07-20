using System.Collections.ObjectModel;
using System.IO;
using TableEditor.Core;
using TableEditor.Services;

namespace TableEditor.ViewModels;

/// <summary>
/// 코드 추출 탭: .bin → C#/C++ 클래스, 전체/선택(-each) 실행.
/// </summary>
public class CodeGenViewModel : ViewModelBase
{
    private readonly ConfigService _cfg;
    private readonly PipelineService _pipe;
    private readonly Action<string> _status;

    private bool _checkAll;
    private bool _withEnum = true;

    public CodeGenViewModel(ConfigService cfg, PipelineService pipe, Action<string> status)
    {
        _cfg = cfg;
        _pipe = pipe;
        _status = status;

        RefreshCommand = new RelayCommand(Refresh, () => !_pipe.IsBusy);
        GenerateAllCommand = new RelayCommand(
            async () => await _pipe.CodeGenAll(_withEnum), () => !_pipe.IsBusy);
        GenerateSelectedCommand = new RelayCommand(
            async () => await GenerateSelected(),
            () => !_pipe.IsBusy && Items.Any(i => i.IsChecked));

        Refresh();
    }

    public ObservableCollection<SelectableItem> Items { get; } = new();

    public string LanguageText =>
        CodeGenerator.ProjectDefine.CodeExtension.EndsWith("cs") ? "C#" : "C++";

    public bool WithEnum
    {
        get => _withEnum;
        set => Set(ref _withEnum, value);
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
    public RelayCommand GenerateAllCommand { get; }
    public RelayCommand GenerateSelectedCommand { get; }

    public void Refresh()
    {
        Items.Clear();

        var dir = string.IsNullOrWhiteSpace(_cfg.CodeGen.BinaryDirectory) ? "Binary" : _cfg.CodeGen.BinaryDirectory;
        dir = Path.GetFullPath(dir);
        if (!Directory.Exists(dir))
        {
            _status($"바이너리 폴더가 없습니다: {dir} (먼저 데이터 추출을 실행하세요)");
            return;
        }

        var pattern = string.IsNullOrWhiteSpace(_cfg.CodeGen.ReadFileExtension) ? "*.bin" : _cfg.CodeGen.ReadFileExtension;
        var names = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>()
            .Distinct()
            .OrderBy(n => n);
        foreach (var name in names)
            Items.Add(new SelectableItem(name));
        _status($"코드 생성 대상 {Items.Count}개 ({dir})");
    }

    private Task GenerateSelected()
    {
        var names = Items.Where(i => i.IsChecked).Select(i => i.Name).ToList();
        return _pipe.CodeGenEach(names, _withEnum);
    }
}

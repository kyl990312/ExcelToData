using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using TableEditor.Core;
using TableEditor.Services;

namespace TableEditor.ViewModels;

/// <summary>
/// 메인 창 루트 VM: 4개 탭 VM + 상태 바 + 로그 패널.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private const int MaxLogChars = 200_000;

    private readonly StringBuilder _log = new();

    private string _statusText = "준비됨";
    private string _logText = string.Empty;
    private bool _isLogOpen;
    private bool _isBusy;

    public MainViewModel(Dispatcher dispatcher)
    {
        var cfg = new ConfigService();
        cfg.LoadAll();

        var pipe = new PipelineService();
        pipe.Status += s => dispatcher.BeginInvoke(() => Status = s);
        pipe.BusyChanged += b => dispatcher.BeginInvoke(() =>
        {
            IsBusy = b;
            CommandManager.InvalidateRequerySuggested();
        });

        UiLog.LineWritten += line => dispatcher.BeginInvoke(() => AppendLog(line));

        void SetStatus(string s) => Status = s;

        Viewer = new ViewerViewModel(cfg, SetStatus);
        Extract = new ExtractViewModel(cfg, pipe, SetStatus);
        CodeGen = new CodeGenViewModel(cfg, pipe, SetStatus);
        Settings = new SettingsViewModel(cfg, SetStatus);

        ClearLogCommand = new RelayCommand(() =>
        {
            _log.Clear();
            LogText = string.Empty;
        });
    }

    public ViewerViewModel Viewer { get; }
    public ExtractViewModel Extract { get; }
    public CodeGenViewModel CodeGen { get; }
    public SettingsViewModel Settings { get; }

    public RelayCommand ClearLogCommand { get; }

    public string Status
    {
        get => _statusText;
        set => Set(ref _statusText, value, nameof(Status));
    }

    public string LogText
    {
        get => _logText;
        private set => Set(ref _logText, value, nameof(LogText));
    }

    public bool IsLogOpen
    {
        get => _isLogOpen;
        set => Set(ref _isLogOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    private void AppendLog(string line)
    {
        _log.AppendLine(line);
        if (_log.Length > MaxLogChars)
            _log.Remove(0, _log.Length - MaxLogChars);
        LogText = _log.ToString();
    }
}

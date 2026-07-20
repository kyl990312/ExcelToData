using System.IO;
using CodeGenerator;
using ExcelToData;
using EnumGenerator;
using GAuth.Core;
using Microsoft.Win32;
using TableEditor.Core;
using TableEditor.Services;

namespace TableEditor.ViewModels;

/// <summary>
/// 설정 탭: 엔진별 Config(JSON) 편집·검증·저장.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ConfigService _cfg;
    private readonly Action<string> _status;

    public SettingsViewModel(ConfigService cfg, Action<string> status)
    {
        _cfg = cfg;
        _status = status;

        SaveCommand = new RelayCommand(Save);
        ValidateCommand = new RelayCommand(Validate);
        DefaultsCommand = new RelayCommand(Defaults);
        BrowseCommand = new RelayCommand(p => Browse(p as string));
    }

    public DataExtractConfig Binary => _cfg.Binary;
    public DataExtractConfig Json => _cfg.Json;
    public CodeGenerateConfig CodeGen => _cfg.CodeGen;
    public EnumGeneratorConfig EnumCfg => _cfg.EnumCfg;
    public UserCredentialConfig GAuthCfg => _cfg.GAuthCfg;

    public string EnumPatternsText
    {
        get => string.Join(",", _cfg.EnumCfg.EnumSheetPatterns);
        set
        {
            _cfg.EnumCfg.EnumSheetPatterns = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            OnPropertyChanged();
        }
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand DefaultsCommand { get; }
    public RelayCommand BrowseCommand { get; }

    private void Save()
    {
        try
        {
            _cfg.SaveAll();
            _status("설정 저장 완료 — 목록은 각 탭에서 [새로고침]으로 반영됩니다.");
        }
        catch (Exception ex)
        {
            UiLog.Write($"[TableEditor][Error] 설정 저장 실패: {ex}");
            _status($"설정 저장 실패: {ex.Message}");
        }
    }

    private void Validate()
    {
        var problems = new List<string>();

        void CheckDir(string label, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                problems.Add($"{label}: 경로가 비어 있음");
            else if (!Directory.Exists(Path.GetFullPath(path)))
                problems.Add($"{label}: 폴더 없음 ({path})");
        }

        if (_cfg.Binary.IsLoadFromLocal)
            CheckDir("데이터 추출 Src", _cfg.Binary.SrcDirectory);
        if (string.IsNullOrWhiteSpace(_cfg.Binary.DstDirectory))
            problems.Add("데이터 추출 Dst: 경로가 비어 있음");
        if (_cfg.Binary.IsLoadFromGSheet && string.IsNullOrWhiteSpace(_cfg.Binary.SrcGDriveLink))
            problems.Add("데이터 추출: GSheet 사용인데 GDrive 링크가 비어 있음");
        CheckDir("코드 생성 Binary", _cfg.CodeGen.BinaryDirectory);

        if (problems.Count == 0)
        {
            _status("검증 통과 — 문제 없음");
        }
        else
        {
            foreach (var p in problems)
                UiLog.Write($"[검증] {p}");
            _status($"검증 경고 {problems.Count}건 — 로그 확인");
        }
    }

    private void Defaults()
    {
        _cfg.ResetDefaults();
        OnPropertyChanged(string.Empty); // 모든 바인딩 갱신
        _status("기본값으로 되돌렸습니다. (저장 전까지 파일은 변경되지 않음)");
    }

    private void Browse(string? key)
    {
        if (key == null)
            return;

        var dlg = new OpenFolderDialog { Title = "폴더 선택" };
        if (dlg.ShowDialog() != true)
            return;

        var path = dlg.FolderName;
        switch (key)
        {
            case "bin.src": _cfg.Binary.SrcDirectory = path; break;
            case "bin.dst": _cfg.Binary.DstDirectory = path; break;
            case "json.src": _cfg.Json.SrcDirectory = path; break;
            case "json.dst": _cfg.Json.DstDirectory = path; break;
            case "cg.bin": _cfg.CodeGen.BinaryDirectory = path; break;
            case "cg.class": _cfg.CodeGen.ClassOutputDirectory = path; break;
        }
        OnPropertyChanged(string.Empty);
    }
}

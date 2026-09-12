using TableAutomationTool.Common.Command;
using TableAutomationTool.ExcelToData;
using TableAutomationTool.Shared;

namespace TableAutomationTool.Vcs;

public enum DiffListStatus
{
    Success,
    PathNotConfigured,
    PathNotFound,
    NotARepository,
    GitNotFound,
    GitFailed
}

/// <summary>
/// git 변경 목록에서 "변경된 테이블" 을 추려 낸다.
/// git 호출 자체는 GitHandler 가 맡고, 엑셀 도메인 필터링만 여기서 한다.
/// </summary>
public class DiffListCommand : Command
{
    private readonly string? _pathOverride;

    public DiffListCommand(string? pathOverride = null)
    {
        _pathOverride = pathOverride;
    }

    public DiffListStatus Status { get; private set; } = DiffListStatus.Success;

    /// <summary>실제로 사용한 대상 폴더 (절대경로)</summary>
    public string ResolvedFolder { get; private set; } = string.Empty;

    /// <summary>"--path" | "GitConfig.RepositoryPath" | "ExcelToDataConfig.SrcDirectory"</summary>
    public string PathSource { get; private set; } = string.Empty;

    /// <summary>경로 출처가 SrcDirectory 인데 설정이 GSheet 전용인 경우 true</summary>
    public bool GSheetOnlySource { get; private set; }

    /// <summary>git 원문 오류 메시지 (오류 상태일 때만 채워진다)</summary>
    public string ErrorDetail { get; private set; } = string.Empty;

    public IReadOnlyList<string> ChangedFiles { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> ChangedTables { get; private set; } = Array.Empty<string>();

    public override void Execute(out string result)
    {
        var baseDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty;

        // 1) GitConfig 로드 (없어도 기본값으로 동작해야 한다)
        var gitConfigPath = Path.Combine(baseDirectory, SystemPath.GitConfigPath);
        if (!FileLoader.LoadAndConvertJson(gitConfigPath, out GitConfig gitConfig))
            gitConfig = GitConfig.GetDefault();

        // 2) 경로 결정: --path > GitConfig.RepositoryPath > ExcelToDataConfig.SrcDirectory
        var folder = ResolveFolder(baseDirectory, gitConfig);
        if (string.IsNullOrWhiteSpace(folder))
        {
            Status = DiffListStatus.PathNotConfigured;
            result = "[DiffListCommand] Target folder is not configured.";
            return;
        }

        ResolvedFolder = Path.GetFullPath(folder);

        if (!Directory.Exists(ResolvedFolder))
        {
            Status = DiffListStatus.PathNotFound;
            result = $"[DiffListCommand] Folder not found: {ResolvedFolder}";
            return;
        }

        // 3) git 조회
        IReadOnlyList<string> files;
        try
        {
            IVcsHandler handler = new GitHandler(gitConfig);
            files = handler.GetDiffList(ResolvedFolder);
        }
        catch (VcsException ex)
        {
            ErrorDetail = ex.Message;
            Status = ex.ErrorType switch
            {
                VcsErrorType.ExecutableNotFound => DiffListStatus.GitNotFound,
                VcsErrorType.NotARepository => DiffListStatus.NotARepository,
                _ => DiffListStatus.GitFailed
            };
            result = $"[DiffListCommand] {Status}: {ex.Message}";
            return;
        }

        // 4) 엑셀 확장자 + Excel 락파일(~$) 필터
        var extensions = ProjectDefine.SearchPatterns
            .Select(p => p.TrimStart('*'))
            .ToArray();

        ChangedFiles = files
            .Where(f => extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
            .ToArray();

        ChangedTables = ChangedFiles
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Status = DiffListStatus.Success;
        result = $"[DiffListCommand] {ChangedTables.Count} changed table(s) in {ResolvedFolder}";
    }

    private string ResolveFolder(string baseDirectory, GitConfig gitConfig)
    {
        if (!string.IsNullOrWhiteSpace(_pathOverride))
        {
            PathSource = "--path";
            return _pathOverride;
        }

        if (!string.IsNullOrWhiteSpace(gitConfig.RepositoryPath))
        {
            PathSource = "GitConfig.RepositoryPath";
            return gitConfig.RepositoryPath;
        }

        var extractConfigPath = Path.Combine(baseDirectory, SystemPath.BinaryConfigPath);
        if (FileLoader.LoadAndConvertJson(extractConfigPath, out DataExtractConfig extractConfig)
            && !string.IsNullOrWhiteSpace(extractConfig.SrcDirectory))
        {
            PathSource = "ExcelToDataConfig.SrcDirectory";
            GSheetOnlySource = extractConfig.IsLoadFromGSheet && !extractConfig.IsLoadFromLocal;
            return extractConfig.SrcDirectory;
        }

        return string.Empty;
    }
}

/// <summary>
/// Config/GitConfig.json 기본 파일 생성. 이미 있으면 건너뛴다.
/// (사용자가 손으로 넣은 RepositoryPath 를 덮어쓰지 않기 위함)
/// </summary>
public class GenerateGitConfigCommand : Command
{
    private readonly string _filePath;

    public GenerateGitConfigCommand(string filePath)
    {
        _filePath = filePath;
    }

    public override void Execute(out string result)
    {
        if (File.Exists(_filePath))
        {
            result = $"[GenerateGitConfigCommand] Config file already exists at: {_filePath}";
            return;
        }

        result = FileLoader.SaveJson(_filePath, GitConfig.GetDefault())
            ? $"[GenerateGitConfigCommand] Generate Config : {_filePath}"
            : $"[GenerateGitConfigCommand] Fail to Create Config : {_filePath}";
    }
}

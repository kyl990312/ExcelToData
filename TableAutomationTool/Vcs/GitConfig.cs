namespace TableAutomationTool.Vcs;

[Serializable]
public class GitConfig
{
    /// <summary>
    /// 대상 저장소(또는 그 하위 폴더) 경로. 비면 ExcelToDataConfig.SrcDirectory 로 폴백한다.
    /// </summary>
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// git 실행 파일 경로. 기본값 "git" (PATH 에서 탐색).
    /// </summary>
    public string GitExecutablePath { get; set; } = "git";

    public static GitConfig GetDefault() => new GitConfig();
}

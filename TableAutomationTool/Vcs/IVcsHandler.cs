namespace TableAutomationTool.Vcs;

/// <summary>
/// 버전 관리 시스템 추상화. 지금은 변경 목록 조회 하나만 제공한다.
/// </summary>
public interface IVcsHandler
{
    /// <summary>
    /// 지정 폴더 하위의 변경된 파일을 fullpath 로 돌려준다. 실패 시 VcsException.
    /// 변경의 정의는 "커밋되지 않은 모든 변경"(staged + unstaged + untracked) 이다.
    /// </summary>
    IReadOnlyList<string> GetDiffList(string folder);
}

/// <summary>
/// VCS 실패 원인 구분. CLI 가 이 값으로 안내 문구와 종료 코드를 정한다.
/// </summary>
public enum VcsErrorType
{
    ExecutableNotFound,
    NotARepository,
    CommandFailed
}

public class VcsException : Exception
{
    public VcsErrorType ErrorType { get; }

    public VcsException(VcsErrorType type, string message) : base(message)
    {
        ErrorType = type;
    }
}

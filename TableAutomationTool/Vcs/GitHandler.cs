using System.Diagnostics;
using System.Text;
using TableAutomationTool.Common.Utils;

namespace TableAutomationTool.Vcs;

/// <summary>
/// git 프로세스를 직접 실행해 변경 목록을 얻는다. (외부 라이브러리 의존 없음)
/// 이 클래스는 "변경 목록을 뽑는 일"만 한다. 프롬프트나 파이프라인 실행은 Command 쪽 책임이다.
/// </summary>
public class GitHandler : IVcsHandler
{
    private readonly GitConfig _config;

    public GitHandler(GitConfig config)
    {
        _config = config;
    }

    /// <summary>실제로 실행할 git 실행 파일 (설정이 비면 PATH 의 "git")</summary>
    private string GitExecutable =>
        string.IsNullOrWhiteSpace(_config.GitExecutablePath) ? "git" : _config.GitExecutablePath;

    public IReadOnlyList<string> GetDiffList(string folder)
    {
        folder = Path.GetFullPath(folder);

        // 1) 저장소 판정 + 루트 획득
        // status --porcelain 의 경로는 항상 저장소 루트 기준이므로 루트를 먼저 알아야 한다.
        var (rc, root, err) = Run(folder, new[] { "-C", folder, "rev-parse", "--show-toplevel" });
        if (rc != 0)
        {
            // rev-parse 실패가 곧 "저장소 아님" 은 아니다.
            // git 이 아닌 실행 파일을 지정했거나 권한 문제일 수도 있으므로 원문으로 구분한다.
            var detail = err.Trim();
            var notARepo = detail.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)
                           || detail.Contains("not a working tree", StringComparison.OrdinalIgnoreCase);

            throw new VcsException(
                notARepo ? VcsErrorType.NotARepository : VcsErrorType.CommandFailed,
                notARepo ? detail : $"'{GitExecutable}' rev-parse exit {rc}: {detail}");
        }

        var repoRoot = root.Trim();

        // 2) 변경 목록 수집
        //    git diff 는 untracked(신규 엑셀 추가)를 절대 보지 못하므로 status 를 쓴다.
        //    -z  : NUL 구분 + 경로를 raw UTF-8 로 출력 (core.quotepath 8진 이스케이프 회피)
        //    -uall : 신규 파일 포착
        //    --no-renames : 이름 변경을 D + A 로 분리해 -z 파싱을 단순하게 유지
        //    -- .  : -C 로 지정한 폴더 하위만 대상
        var (rc2, outText, err2) = Run(folder, new[]
        {
            "-C", folder,
            "status", "--porcelain=v1", "-z",
            "--untracked-files=all", "--no-renames",
            "--", "."
        });

        if (rc2 != 0)
            throw new VcsException(VcsErrorType.CommandFailed, $"exit {rc2}: {err2.Trim()}");

        // 3) 파싱: "XY<공백>path\0" 반복
        var list = new List<string>();
        foreach (var entry in outText.Split('\0'))
        {
            if (entry.Length < 4)
                continue;

            var rel = entry.Substring(3);   // 고정 3바이트 헤더(XY + 공백) 제거
            var full = Path.GetFullPath(Path.Combine(repoRoot, rel));

            // 삭제·이동된 원본은 실제 파일이 없으므로 여기서 자동 탈락한다.
            if (!File.Exists(full))
                continue;

            list.Add(full);
        }

        var result = list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        // 결과가 비었을 때 그것이 "정말 변경이 없어서"인지 ".gitignore 로 무시당해서"인지 구분해 준다.
        // 테이블 폴더가 ignore 되어 있으면 git status 가 아무것도 내놓지 않아 두 경우가 똑같이 보인다.
        if (result.Length == 0)
            WarnIfIgnored(folder);

        return result;
    }

    /// <summary>
    /// 변경이 0건일 때, 대상 폴더에 .gitignore 로 무시되는 파일이 있으면 경고한다.
    /// </summary>
    private void WarnIfIgnored(string folder)
    {
        var (rc, outText, _) = Run(folder, new[]
        {
            "-C", folder,
            "status", "--porcelain=v1", "-z",
            "--untracked-files=all", "--ignored=matching", "--no-renames",
            "--", "."
        });

        if (rc != 0)
            return;

        var ignored = outText.Split('\0')
            .Where(e => e.Length >= 4 && e.StartsWith("!!", StringComparison.Ordinal))
            .Select(e => e.Substring(3))
            .Take(5)
            .ToList();

        if (ignored.Count == 0)
            return;

        Logger.Instance.Warning(
            $"[GitHandler] 변경 0건이지만 .gitignore 로 무시되는 파일이 있습니다: {string.Join(", ", ignored)}"
            + " — 테이블 폴더가 .gitignore 에 걸려 있으면 변경을 감지할 수 없습니다.");
    }

    private (int ExitCode, string StdOut, string StdErr) Run(string workingDir, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GitExecutable,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };

        // 경로에 공백이 들어갈 수 있으므로 ArgumentList 를 쓴다.
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                throw new VcsException(VcsErrorType.ExecutableNotFound, psi.FileName);

            // WaitForExit 이전에 읽어 파이프 데드락을 피한다.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            process.WaitForExit();

            return (process.ExitCode, stdout, stderr);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new VcsException(VcsErrorType.ExecutableNotFound, ex.Message);
        }
    }
}

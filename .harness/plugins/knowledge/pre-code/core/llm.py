"""claude -p 래퍼 (stub off일 때 analyze/update가 사용)."""
import shutil
import subprocess


def _claude_exe():
    # Windows: npm 심(claude.CMD)은 리스트 인자로 직접 실행 불가 → which로 전체 경로 해석.
    return shutil.which("claude") or "claude"


def run(prompt, model, cwd):
    cmd = [_claude_exe(), "-p", "--model", model, "--permission-mode", "bypassPermissions"]
    subprocess.run(cmd, input=prompt, text=True, encoding="utf-8", errors="replace",
                   cwd=cwd, check=False)

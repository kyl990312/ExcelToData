"""build / test / commit 실행 = 코드. exit code가 진실 (AI 아님)."""
import os
import shlex
import subprocess

import context
import markers


def _run_shell(cmd_str, env=None, cwd=None):
    """build/test용: 빈 명령 = 더미 pass(0). 아니면 OS 셸로 실행(임의 명령·파이프·경로 허용)."""
    if not cmd_str:
        return 0
    r = subprocess.run(cmd_str, shell=True, cwd=cwd or context.HARNESS_ROOT, env=env)
    return r.returncode


def _run_argv(cmd_str, extra_arg, env=None, cwd=None):
    """commit용: 베이스 명령은 토큰화(Windows 경로 보존), extra_arg(메시지)는 깨끗한 argv로 추가."""
    if not cmd_str:
        return 0
    args = shlex.split(cmd_str, posix=False)
    if extra_arg is not None:
        args.append(extra_arg)
    r = subprocess.run(args, cwd=cwd or context.HARNESS_ROOT, env=env)
    return r.returncode


def build_test(item, cfg):
    bc = _run_shell(cfg.get("build_cmd", ""))
    if bc != 0:
        markers.write_test_result(item, False, bc)
        return False
    tc = _run_shell(cfg.get("test_cmd", ""))
    markers.write_test_result(item, tc == 0, tc)
    return tc == 0


def run_commit(item, cfg):
    msg = markers.read_commit_msg(item) or ("harness: complete %s" % item)
    env = dict(os.environ)
    repo = cfg.get("commit_repo_dir") or "."
    env["HARNESS_COMMIT_REPO_DIR"] = os.path.abspath(os.path.join(context.HARNESS_ROOT, repo))
    code = _run_argv(cfg.get("commit_cmd", ""), msg, env=env)
    markers.write_commit_result(item, code == 0, code)
    return code == 0


def run_finalize(item, cfg, ds):
    """finalize 단계: 메시지+문서 작성 → 커밋 → docs_written 확인."""
    import stages
    stages.run(item, "finalize", cfg, ds)
    committed = run_commit(item, cfg)
    docs_written = ds.exists("report/" + item)
    return committed, docs_written

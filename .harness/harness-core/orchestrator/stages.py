"""AI 단계 실행 래퍼.

stub_stages=true: claude 호출 없이 캐넌 산출물 기록 (M0~M2 스파인 검증, 토큰 0).
stub_stages=false: stages/<name>.md 프롬프트로 `claude -p` 서브프로세스 호출 (M3+).

전환 진실은 단계가 남긴 '검증된 산출물 파일'이지 AI 발화가 아니다.
"""
import os
import shutil
import subprocess

import context
import config as cfgmod
import markers


def run(item, name, cfg, ds):
    if cfg.get("stub_stages"):
        return _run_stub(item, name, ds)
    return _run_claude(item, name, cfg, ds)


# --- 스텁 (스파인 검증용) ---

def _run_stub(item, name, ds):
    params = markers.read_params(item)
    if name == "plan":
        ds.write("plan/" + item, "# Plan: %s\n\n(stub plan)\n" % item)
    elif name == "plan-review":
        approved = not params.get("force_plan_reject", False)
        markers.write_verdict(item, "plan", approved, "stub plan-review")
    elif name == "dev":
        ds.write("report/" + item, "# Dev report: %s\n\n(stub draft)\n" % item)
    elif name == "review":
        approved = not params.get("force_review_reject", False)
        markers.write_verdict(item, "review", approved, "stub review")
    elif name == "finalize":
        markers.write_commit_msg(item, "harness: complete %s" % item)
        ds.write("report/" + item, "# Dev report: %s\n\n(stub final)\n" % item)


# --- 실제 claude -p ---

def _stage_prompt(name):
    with open(os.path.join(context.STAGES_DIR, name + ".md"), "r", encoding="utf-8") as f:
        return f.read()


def _render(template, mapping):
    out = template
    for k, v in mapping.items():
        out = out.replace("{{" + k + "}}", str(v))
    return out


def _context_block(item, name):
    lines = [
        "## 실행 컨텍스트 (orchestrator 주입)",
        "- ITEM: %s" % item,
        "- HARNESS_ROOT(=cwd): %s" % context.HARNESS_ROOT,
        "- 계획 문서 ref: plan/%s  → docs/plan/%s.md" % (item, item),
        "- 리포트 문서 ref: report/%s → docs/report/%s.md" % (item, item),
        "- task 디렉터리: task/%s/" % item,
    ]
    return "\n".join(lines)


def _claude_exe():
    # Windows: npm 심(claude.CMD)은 리스트 인자로 직접 실행 불가 → which로 전체 경로 해석.
    return shutil.which("claude") or "claude"


def _run_claude(item, name, cfg, ds):
    # knowledge는 단계 프롬프트가 /knowledge-agent-* 스킬로 호출한다(하네스 core는 관여 안 함).
    prompt = _render(_stage_prompt(name), {"item": item}) + "\n\n" + _context_block(item, name)
    model = cfgmod.model_for(cfg, name)
    # 무인 실행: 중첩 스테이지는 도구를 자유로이 써야 하므로 권한 우회. 전환 진실은 산출물 파일이다.
    cmd = [_claude_exe(), "-p", "--model", model, "--permission-mode", "bypassPermissions"]
    timeout = cfg.get("stage_timeout_sec") or 900
    try:
        subprocess.run(cmd, input=prompt, text=True, encoding="utf-8", errors="replace",
                       cwd=context.HARNESS_ROOT, check=False, timeout=timeout)
    except subprocess.TimeoutExpired:
        # 산출물 미생성으로 귀결 → loop의 검증에서 escalate. 여기서 크래시하지 않는다.
        pass

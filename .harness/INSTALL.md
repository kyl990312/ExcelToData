# 개발 AI 하네스 — 적용 가이드

## 적용 방법
배포본의 **세 가지를 프로젝트 루트에 복사**한다(셋이 같은 위치 = 루트):
```
<project>/
├── CLAUDE.md      ← 디스패처 (Claude Code가 루트에서 읽음)
├── .claude/       ← skills(start-task·approve·finalize·harness·knowledge-agent-*) + settings
└── .harness/      ← 엔진(harness-core·config·knowledge·plugins·docs) + 이 INSTALL.md
```
→ **프로젝트 루트에서 Claude Code를 실행하면 하네스가 자동 적용된다**(루트 `.claude`/`CLAUDE.md` 읽음).

## 요구사항
Python 3.x(외부 패키지 0) · git · `claude` CLI(헤드리스 단계) · Windows면 powershell(commit).

## 프로젝트마다 채우는 값
1. `.claude/settings.local.json` — `<PROJECT_ROOT>`를 실제 프로젝트 절대경로로 치환.
2. `.harness/config/harness.json`:
   - `build_cmd`/`test_cmd` · `commit_cmd`/`commit_repo_dir`(빈값=더미 pass)
   - **`stub_stages: false`**(실제 `claude -p`로 돌릴 때; 기본 true=스텁)
   - `mode: default|auto` (또는 토글 `run.py auto on|off`)
3. (선택) knowledge 맵 부트스트랩: `python .harness/plugins/knowledge/run.py analyze`
4. (선택) pre-code 데이터 위치: `.harness/plugins/knowledge/pre-code/config.json`의 `workspace`(기본 `.pre-code`).

## 구조 / 개념
- **하네스 core**(`.harness/harness-core`)는 orchestrator(코드)가 마커로 Phase를 결정적으로 전진.
- **knowledge = plugin**(`.harness/plugins/knowledge` = adapter + pre-code). AI 단계가 `/knowledge-agent-*`
  스킬로 호출 → adapter가 plugin들로 라우팅. core는 knowledge를 모름(착탈).
- **데이터/기능 분리**: pre-code 데이터(코드 맵)는 plugin config의 워크스페이스(`.pre-code`, 프로젝트 루트).

## 들고 오지 않는 것 (런타임 생성·gitignore)
`.harness/task/`(마커) · `.harness/docs/plan`·`docs/report` · `__pycache__` · `.pre-code/`(knowledge 데이터).

## 사용
```
/start-task <item> → /approve → /approve → /finalize → /approve   # done
python .harness/harness-core/orchestrator/run.py auto on          # 게이트 스킵, 끝까지 자동
```

## 알려진 한계(실모드 = stub off에서 확인 필요)
- orchestrator가 `claude -p` 단계를 **`.harness/` cwd**로 실행하므로, 그 단계가 `/knowledge-agent-*`
  스킬(명령은 `.harness/` 접두, 루트 기준)을 호출할 때 cwd 불일치가 날 수 있다. 대화형(루트에서 직접
  스킬 실행)·stub 모드는 문제없음. 실 LLM 단계 연동은 한 번 검증·조정 필요.

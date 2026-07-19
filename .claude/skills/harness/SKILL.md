---
name: harness
description: 개발 AI 하네스의 사용법·구조·원칙. 하네스로 작업하거나 동작이 궁금할 때 로드.
disable-model-invocation: false
---

# 개발 AI 하네스 (developer harness)

작업 아이템 1개를 `planning → dev-loop → user-adjust → finalizing → done`으로 통과시키는
코드 주도 개발 하네스. 엔진은 `.harness/`.

## 핵심 원칙
- **전환은 코드가 결정.** `.harness/harness-core/orchestrator`(Python)가 `.harness/task/` 마커를 읽어
  현재 Phase 판정·전진. 세션이 임의로 다음 단계를 자가호출하지 않는다.
- **AI는 단계 작업만**(plan·plan-review·dev·review·finalize). 각 단계는 orchestrator가 `claude -p`로
  별도 실행(`stub_stages: true`면 캐넌 산출물로 대체).
- **build/test/commit은 코드가 실행**, exit code가 진실.

## 슬래시 명령
| 명령 | 의미 |
|---|---|
| `/start-task <item>` | 큐 등록 + planning 시작 → 게이트 정지 |
| `/approve [item]` | 현재 Phase 게이트 통과 → 다음 Phase 후 정지 |
| `/finalize [item]` | user-adjust 종료 → finalizing |

직접: `python .harness/harness-core/orchestrator/run.py <start|approve|finalize|advance|status> [item]`
**auto 모드**: `python .harness/harness-core/orchestrator/run.py auto on|off|status` — ON이면 사람
게이트 전부 스킵, `start` 한 번에 `done`까지(결정적 종료사실은 auto여도 안 건너뜀).

## knowledge (plugin)
코드/문서 위치는 **knowledge agent**(plugin)에게 스킬로 물어본다:
`/knowledge-agent-search`(코드 찾기) · `/knowledge-agent-update-code`(변경 반영) ·
`/knowledge-agent-analyze`(전체 분석). 구현은 `.harness/plugins/knowledge/`(adapter + pre-code plugin),
데이터는 plugin `config.json`의 워크스페이스(기본 `.pre-code`, 프로젝트 데이터). 하네스 core와 분리.

## 구조 (`.harness/`)
- `harness-core/` — orchestrator·ports·adapters·stages·guardrails·scripts·CONTRACT.md
- `plugins/knowledge/` — knowledge adapter(`run.py`) + plugin(`pre-code`)
- `knowledge/criteria/` — stage 심사 기준(plan-review·review)
- `task/` 마커 · `docs/` 문서 · `config/harness.json` 설정

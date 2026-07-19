# CONTRACT — 신호 · 전환 (코드가 따르는 단일 기준)

명세 §3을 코드 관점으로 옮긴 것. orchestrator는 **여기 정의된 검사 가능한 사실(마커·exit code·
verdict)에만** 근거해 전환한다. AI 자기보고는 전환 근거가 아니다.

## 1. 검사 가능한 사실 (signals)

| signal | 진실(로컬) |
|---|---|
| `start_marker` | `task/queue/<item>.json` |
| `plan_ready` | DocStore `exists(plan/<item>)` (= `docs/plan/<item>.md`) |
| `plan.approved` | `task/<item>/plan.verdict.json` `approved:true` |
| `review.approved` | `task/<item>/review.verdict.json` `approved:true` |
| `test_passed` / `build_failed` | `test.result.json` `passed` (build/test exit code 0 / ≠0) |
| `committed` | `commit.result.json` `committed` (commit_cmd exit code 0; 메시지는 finalize가 작성한 arg) |
| `docs_written` | DocStore `exists(report/<item>)` |
| `user_approved@<phase>` | `task/<item>/approvals/<phase>` 존재 |
| `human_approved` | `task/<item>/human_approved` 존재 |
| `done` / `escalated` | `task/<item>/done` / `escalated` 존재 |

## 2. Phase 전환표 (default 모드)

| 전환 | 종료 사실 (+게이트) |
|---|---|
| `(start) → planning` | `start_marker` |
| `planning` 내부 | `plan ⟷ plan-review` → `plan.approved` (+캡) |
| `planning → dev-loop` | `plan.approved` AND `user_approved@planning` |
| `dev-loop` 내부 | `review.approved` → `build-test` → `test_passed` (+캡, 실패 시 `dev` 재진입) |
| `dev-loop → user-adjust` | `review.approved` AND `test_passed` AND `user_approved@dev-loop` |
| `user-adjust → finalizing` | `human_approved` (`/finalize`) |
| `finalizing → done` | `committed` AND `docs_written` AND `user_approved@finalizing` |

> **knowledge는 plugin**(`plugins/knowledge` = adapter + pre-code/docs[차후])으로 분리됨. AI 단계가
> `/knowledge-agent-search`(plan·dev)·`/knowledge-agent-update-code`(finalize)·`/knowledge-agent-analyze`
> 스킬로 호출 → 스킬이 adapter(`plugins/knowledge/run.py`) 실행 → plugin들로 라우팅·취합. 하네스 core는
> knowledge를 모른다(착탈식). 데이터(코드 맵)는 plugin `config.json`이 가리키는 외부 워크스페이스(하네스 밖).

## 3. Task 전환 규칙 (loop.py)

- planning: `plan → plan-review → (approved? 종료 : plan)` + 캡(planning).
- dev-loop: `dev → review → (approved? build-test : dev)` → `build-test → (passed? 종료 : dev)`
  + 캡(dev-loop, **리뷰 반복 + 빌드테스트 재시도 합산**).
- finalizing: `finalize`(커밋 메시지 작성) → 커밋(commit_cmd, exit code) → `docs_written`.

## 4. 마커 스키마 (task/)

```
task/
  queue/<item>.json       { item_id, params, ts }
  <item>/
    phase                 planning|dev-loop|user-adjust|finalizing|done
    counters.json         { planning, dev-loop }            # 캡 합산
    plan.verdict.json     { approved, notes }               # plan-review(AI) 작성
    review.verdict.json   { approved, notes }               # review(AI) 작성
    test.result.json      { passed, exit_code }             # runners(코드) 작성
    commit_msg.txt        finalize(AI) 작성 → commit_cmd arg
    commit.result.json    { committed, exit_code }          # runners(코드) 작성
    approvals/<phase>      존재 = user_approved@phase
    human_approved         존재 = /finalize
    done | escalated       존재 = 해당 상태 (+ escalated.json{phase,reason})
```

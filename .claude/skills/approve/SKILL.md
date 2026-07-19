---
name: approve
description: 현재 Phase 게이트를 사람 승인으로 통과시키고 오케스트레이터를 다음 Phase로 전진시킨다.
disable-model-invocation: true
---

# /approve

사용법: `/approve [item]` — item 생략 시 진행 중인 단일 아이템에 적용.

1. 프로젝트 루트에서 실행한다:
   ```bash
   python .harness/harness-core/orchestrator/run.py approve "<item>"
   ```
   (item을 모르면 인자 없이 `... run.py approve` — active item 자동 선택)
2. 출력된 JSON을 사용자에게 보고한다. 다음 게이트에서 다시 멈추거나 `done`이면 알린다.

> user-adjust 단계에서는 `/approve`가 아니라 `/finalize`를 쓴다.

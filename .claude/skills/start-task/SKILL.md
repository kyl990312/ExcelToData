---
name: start-task
description: 새 작업 아이템을 큐에 등록하고 개발 하네스 오케스트레이터를 기동한다(planning 시작, 게이트에서 정지).
disable-model-invocation: true
---

# /start-task

사용법: `/start-task <item>` — `<item>`은 작업 아이템 id.

1. 인자에서 item id를 확보한다. 없으면 사용자에게 묻는다.
2. 프로젝트 루트에서 실행한다:
   ```bash
   python .harness/harness-core/orchestrator/run.py start "<item>"
   ```
3. 출력된 JSON(`phase`·`status`·`gate`)을 사용자에게 그대로 보고한다.
4. `status`가 `AWAITING_APPROVAL`이면, `/approve`로 게이트를 통과시켜야 다음 Phase로 간다고 안내한다.

> 마커를 기록하고 orchestrator를 호출할 뿐, 전환 판단은 orchestrator(코드)가 한다.

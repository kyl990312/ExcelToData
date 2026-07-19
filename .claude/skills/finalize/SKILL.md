---
name: finalize
description: user-adjust 단계를 종료하고(human_approved) finalizing(커밋 메시지 작성→커밋→문서갱신)으로 전진시킨다.
disable-model-invocation: true
---

# /finalize

사용법: `/finalize [item]` — item 생략 시 진행 중인 단일 아이템에 적용.

1. 프로젝트 루트에서 실행한다:
   ```bash
   python .harness/harness-core/orchestrator/run.py finalize "<item>"
   ```
2. 출력된 JSON을 사용자에게 보고한다. finalizing이 끝나면 finalizing 게이트에서 멈추므로,
   마지막으로 `/approve`를 한 번 더 해야 `done`이 된다고 안내한다.

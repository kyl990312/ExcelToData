---
name: knowledge-agent-update-code
description: 코드 변경사항을 knowledge(pre-code) 맵에 반영한다. finalize 단계에서 변경 마무리 시 사용.
disable-model-invocation: false
---

# /knowledge-agent-update-code

이번 작업의 코드 변경을 knowledge 맵에 반영한다.

사용법: `/knowledge-agent-update-code <변경 요약>`

1. 프로젝트 루트에서 실행:
   ```bash
   python .harness/plugins/knowledge/run.py update-code "<변경 요약>"
   ```
2. 결과를 보고한다.

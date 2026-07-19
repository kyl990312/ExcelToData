---
name: knowledge-agent-analyze
description: 프로젝트 전체를 분석해 knowledge 맵을 처음부터 작성한다(부트스트랩). knowledge agent 전용.
disable-model-invocation: false
---

# /knowledge-agent-analyze

프로젝트 전체를 풀스캔해 knowledge 맵을 작성한다(부트스트랩/재구축).

사용법: `/knowledge-agent-analyze [프로젝트 루트]`

1. 프로젝트 루트에서 실행:
   ```bash
   python .harness/plugins/knowledge/run.py analyze "[루트]"
   ```
2. 결과를 보고한다.

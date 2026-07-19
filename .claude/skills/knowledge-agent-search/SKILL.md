---
name: knowledge-agent-search
description: 작업에 필요한 코드(이후 문서 등)를 knowledge 플러그인에서 찾아 반환한다. plan·dev 작업 중 코드 위치가 필요할 때 사용.
disable-model-invocation: false
---

# /knowledge-agent-search

작업에 필요한 코드 위치/관련 API를 knowledge agent에게 물어본다.

사용법: `/knowledge-agent-search <작업 또는 질의>`

1. 프로젝트 루트에서 실행:
   ```bash
   python .harness/plugins/knowledge/run.py search "<질의>"
   ```
2. 출력(plugin별 취합 결과 — pre-code 등)을 활용해 대상 코드 위치/API를 파악한다.
3. 결과가 비었으면 코드를 직접 탐색한다.

> knowledge adapter가 등록된 plugin(`.harness/plugins/knowledge/*`)으로 라우팅·취합한다.

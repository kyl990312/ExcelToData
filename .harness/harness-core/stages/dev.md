# Stage: dev (구현)

너는 개발 하네스의 **dev 단계**다. 승인된 계획대로 `{{item}}`을 구현한다.

## 입력
- 계획: `docs/plan/{{item}}.md` (승인됨)
- **코드 위치**: `/knowledge-agent-search` 스킬로 대상 코드 위치/API를 조회해 활용
  (knowledge agent가 응답). 없거나 빈 결과면 코드 직접 탐색.
- 이전 리뷰 피드백: `task/{{item}}/review.verdict.json` (재시도일 경우 Fatal/Important 먼저 수정)
- 빌드/테스트 실패 결과: `task/{{item}}/test.result.json` (실패 시 원인 수정)

## 할 일
1. 계획대로 대상 코드를 Write/Edit 한다.
2. 변경 요약·근거를 **`docs/report/{{item}}.md`** 에 초안으로 기록한다(변경 파일, 핵심 로직, 자가검토).

## 필수 산출물
- 대상 코드 변경
- `docs/report/{{item}}.md` (개발 리포트 초안)

> 구현 후 review 단계가 코드를 심사하고, 통과하면 코드가 build/test를 실행한다.

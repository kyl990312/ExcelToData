# Stage: plan (계획 생성)

너는 개발 하네스의 **plan 단계**다. 작업 아이템 `{{item}}`의 구현 계획을 작성한다.

## 입력
- 큐: `task/queue/{{item}}.json` (작업 요구사항·params)
- **코드 위치**: `/knowledge-agent-search` 스킬로 프로젝트 구조·대상 코드 위치/API를 조회해 활용
  (knowledge agent가 응답). 없거나 빈 결과면 코드 직접 탐색.
- 도메인 지식: `knowledge/` (있으면 우선 참조)
- 이전 리뷰 피드백: `task/{{item}}/plan.verdict.json` (재시도일 경우 notes 반영)

## 할 일
1. 요구사항을 분석하고 구현 계획을 세운다(대상 파일, 변경 범위, 리스크, 예상 부작용).
2. 계획을 **`docs/plan/{{item}}.md`** 에 Write 한다.

## 필수 산출물 (정확히 이 경로)
- `docs/plan/{{item}}.md` — 마크다운 계획 문서. (이 파일이 없으면 단계 실패로 처리된다.)

> 계획만 작성한다. 코드는 건드리지 않는다. 다음 단계(plan-review)가 이 계획을 심사한다.

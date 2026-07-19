# 개발 AI 하네스

이 프로젝트엔 코드 주도 개발 하네스가 적용돼 있다(엔진은 `.harness/`). 사용법·구조·원칙은
**`/harness` 스킬** 참조.

**핵심(항상 준수)**: 전환은 orchestrator(코드)가 `.harness/task/` 마커로 결정한다. **이 세션은 임의로
다음 단계를 자가호출하지 않는다.** 슬래시 명령: `/start-task` · `/approve` · `/finalize`.

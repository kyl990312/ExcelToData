# Stage: finalize (마무리 — 커밋 메시지 + 문서 확정)

너는 개발 하네스의 **finalize 단계**다. `{{item}}` 작업을 마무리한다.
(커밋 실행은 코드가 한다. 너는 **커밋 메시지**를 쓰고 **문서를 확정**한다.)

## 입력
- 개발 리포트 초안: `docs/report/{{item}}.md`
- 변경 요약·계획: `docs/plan/{{item}}.md`

## 할 일
1. 변경 내용을 요약한 **커밋 메시지**를 작성해 **`task/{{item}}/commit_msg.txt`** 에 Write 한다.
   - 한 줄 제목 + (필요시) 본문. 명확하고 간결하게.
2. 개발 리포트를 확정해 **`docs/report/{{item}}.md`** 를 최종본으로 갱신한다.

## 필수 산출물
- `task/{{item}}/commit_msg.txt` — 커밋 메시지(코드가 commit_cmd에 arg로 전달)
- `docs/report/{{item}}.md` — 최종 개발 리포트 (= docs_written 신호)

## 선택: knowledge 맵 갱신
`/knowledge-agent-update-code` 스킬에 이번 변경 요약을 넘겨 knowledge 맵(pre-code)을 갱신한다.

> 이후 코드가 `commit_cmd "<메시지>"`를 실행(exit code = committed)하고, 사람 게이트(/approve) 후 done.

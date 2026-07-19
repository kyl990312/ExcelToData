# review 세부 기준

review(코드 리뷰) 단계가 참조하는 코드 심사 기준. (프로젝트가 채워 넣는다.)

분류:
- 치명(Fatal): 시스템 장애·보안 결함 — 수정 필수
- 중요(Important): 로직 오류·컨벤션 위반 — 수정 필수
- 권장(Recommended): 가독성·리팩토링 — 선택

approved = (Fatal == 0 AND Important == 0).

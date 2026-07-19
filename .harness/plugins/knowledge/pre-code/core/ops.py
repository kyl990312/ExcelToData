"""pre-code 3대 오퍼레이션: search / update / analyze.

stub=true면 claude 호출 없이 캐넌 동작(검증용). 데이터는 ctx.workspace_dir(cfg)에 산다.
작성 규약(요지): 카테고리=독립 기능 단위 / 카테고리마다 Index / per-file md는 '구현 요약 + API'만(코드 복붙 금지).
"""
import ctx
import store
import llm

_STUB_CATEGORY = (
    "# pre-code Category Map\n\n"
    "| 카테고리 | 소스경로 | 지식폴더 | 역할 | 의존(참조) |\n"
    "|---|---|---|---|---|\n"
    "| Sample | src/Sample | ./Sample/ | 예시 카테고리 | - |\n"
)

_AUTHORING = (
    "작성 규약: 카테고리=독립 기능 단위(과/소분할 금지), 카테고리마다 Index.md, "
    "per-file md는 '구현 요약 + API 문서'만(전체 코드 복붙 금지), Category.md에 의존(참조) 기록."
)


def search(cfg, query):
    ws = ctx.workspace_dir(cfg)
    dumped = store.dump(ws)
    if cfg.get("stub") or not dumped.strip():
        return ("pre-code workspace=%s\n%s\n\n[빈/스텁 맵] query=%r 관련 항목 없음 → 코드 직접 탐색 폴백."
                % (ws, dumped or "(빈 맵)", query))
    return ("pre-code map (workspace=%s):\n%s\n\n질의 %r 와 관련된 카테고리/파일 위치를 위 맵에서 특정하라."
            % (ws, dumped, query))


def update(cfg, summary):
    ws = ctx.workspace_dir(cfg)
    if cfg.get("stub"):
        store.write_category(ws, _STUB_CATEGORY)
        store.write_index(ws, "Sample", "# Sample Index\n\n| 파일 | 경로 | 핵심역할 | md |\n|---|---|---|---|\n")
        return "pre-code updated (stub). workspace=%s" % ws
    prompt = (
        "너는 pre-code 맵 관리자다. 아래 변경 요약에 맞춰 `{ws}`의 코드 검색 맵을 **증분 갱신**한다.\n\n"
        "절차:\n"
        "1. 변경된 파일이 속한 카테고리의 `{ws}/<카테고리>/Index.md`와 해당 `<파일명>.md`를 갱신.\n"
        "2. 새 독립 기능 단위면 `{ws}/Category.md`에 카테고리 등록 + 폴더/Index 생성.\n"
        "3. per-file md는 '구현 요약 + API'만 유지(코드 복붙 금지).\n\n"
        "{authoring}\n\n"
        "[변경 요약]\n{summary}\n\n위 경로들에 Write 도구로 직접 쓴다."
    ).format(ws=ws, authoring=_AUTHORING, summary=summary)
    llm.run(prompt, cfg["model"], ws)
    return "pre-code update invoked (workspace=%s)" % ws


def analyze(cfg, root):
    ws = ctx.workspace_dir(cfg)
    target = root or "."
    if cfg.get("stub"):
        store.write_category(ws, _STUB_CATEGORY)
        return "pre-code analyzed (stub). workspace=%s" % ws
    prompt = (
        "너는 pre-code 맵 관리자다. 프로젝트 루트 `{root}`의 코드를 전체 분석해 `{ws}`(절대경로)에\n"
        "'코드 검색 맵'을 작성한다. 목적: 다른 에이전트가 작업에 필요한 코드를 빠르게 찾는 인덱스.\n\n"
        "절차:\n"
        "1. `{root}`를 훑어 소스 파일을 파악하고 **독립 기능 단위 = 카테고리**로 분류.\n"
        "2. `{ws}/Category.md` 작성 — 표 헤더: | 카테고리 | 소스경로 | 지식폴더 | 역할 | 의존(참조) |\n"
        "3. 카테고리마다 `{ws}/<카테고리>/Index.md` — 표 헤더: | 파일 | 상대경로 | 핵심역할 | 문서(md) |\n"
        "4. 주요 파일마다 `{ws}/<카테고리>/<파일명>.md` — ① 구현 요약(1~3문장) ② API(공개 시그니처·용도)."
        " 전체 코드 복붙 금지.\n\n"
        "{authoring}\n\n"
        "위 경로들에 Write 도구로 직접 쓴다. 끝나면 작성한 카테고리/파일 수를 한 줄로 보고."
    ).format(root=target, ws=ws, authoring=_AUTHORING)
    llm.run(prompt, cfg["model"], target)
    return "pre-code analyze invoked (root=%s, workspace=%s)" % (target, ws)

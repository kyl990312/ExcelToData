"""워크스페이스(코드 검색 맵) 입출력. Category.md → <Category>/Index.md → <Category>/<file>.md."""
import os


def _ensure(d):
    os.makedirs(d, exist_ok=True)


def category_path(ws):
    return os.path.join(ws, "Category.md")


def read_category(ws):
    p = category_path(ws)
    return open(p, "r", encoding="utf-8").read() if os.path.exists(p) else ""


def write_category(ws, text):
    _ensure(ws)
    with open(category_path(ws), "w", encoding="utf-8") as f:
        f.write(text)


def write_index(ws, category, text):
    d = os.path.join(ws, category)
    _ensure(d)
    with open(os.path.join(d, "Index.md"), "w", encoding="utf-8") as f:
        f.write(text)


def write_file_doc(ws, category, filename, text):
    d = os.path.join(ws, category)
    _ensure(d)
    with open(os.path.join(d, filename + ".md"), "w", encoding="utf-8") as f:
        f.write(text)


def dump(ws):
    """워크스페이스 전체를 텍스트로(검색 결과 반환용)."""
    if not os.path.isdir(ws):
        return ""
    out = [read_category(ws)]
    for name in sorted(os.listdir(ws)):
        idx = os.path.join(ws, name, "Index.md")
        if os.path.isfile(idx):
            out.append("\n## [%s] Index\n%s" % (name, open(idx, encoding="utf-8").read()))
    return "\n".join(t for t in out if t.strip())

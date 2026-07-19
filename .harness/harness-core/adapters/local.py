"""LocalDocStore — DocStore 포트의 로컬 파일 어댑터.

DocRef("plan/<item>") -> {root}/plan/<item>.md
"""
import os

from ports.doc_store import DocStore


class LocalDocStore(DocStore):
    def __init__(self, root):
        self.root = root

    def _path(self, ref):
        return os.path.join(self.root, ref.replace("/", os.sep) + ".md")

    def read(self, ref):
        p = self._path(ref)
        if not os.path.exists(p):
            return None
        with open(p, "r", encoding="utf-8") as f:
            return f.read()

    def write(self, ref, text):
        p = self._path(ref)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8") as f:
            f.write(text)

    def exists(self, ref):
        return os.path.exists(self._path(ref))

    def list(self, prefix):
        base = os.path.join(self.root, prefix.replace("/", os.sep))
        if not os.path.isdir(base):
            return []
        return ["%s/%s" % (prefix, f[:-3]) for f in os.listdir(base) if f.endswith(".md")]

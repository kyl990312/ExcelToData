"""knowledge adapter — orchestrator(AI 단계)가 스킬로 호출하는 knowledge 인터페이스의 구현.

search/update-code/analyze를 **등록된 plugin들로 라우팅하고 결과를 취합**한다.
plugin = 하위 디렉터리 중 `plugin.py`(표준 IF: search/update_code/analyze)를 가진 것(pre-code, docs[차후]).
knowledge는 여러 개가 될 수 있다(여러 plugin 취합).
"""
import os
import sys
import importlib.util

_HERE = os.path.dirname(os.path.abspath(__file__))
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass


def _load_plugins():
    mods = []
    for name in sorted(os.listdir(_HERE)):
        pfile = os.path.join(_HERE, name, "plugin.py")
        if os.path.isfile(pfile):
            spec = importlib.util.spec_from_file_location("kplugin_%s" % name.replace("-", "_"), pfile)
            mod = importlib.util.module_from_spec(spec)
            try:
                spec.loader.exec_module(mod)
                mods.append((name, mod))
            except Exception as e:
                sys.stderr.write("[knowledge-adapter] plugin load failed (%s): %s\n" % (name, e))
    return mods


def _fanout(method, arg):
    out = []
    for name, mod in _load_plugins():
        fn = getattr(mod, method, None)
        if fn is None:
            continue
        label = getattr(mod, "NAME", name)
        out.append("### [%s]\n%s" % (label, fn(arg)))
    return "\n\n".join(out) if out else "(해당 작업을 지원하는 knowledge plugin 없음)"


def search(query):
    return _fanout("search", query)


def update_code(changes):
    return _fanout("update_code", changes)


def analyze(root):
    return _fanout("analyze", root or None)


def main(argv):
    if not argv:
        print("usage: run.py search|update-code|analyze [args]")
        return 2
    op = argv[0]
    arg = " ".join(argv[1:])
    if op == "search":
        print(search(arg))
    elif op == "update-code":
        print(update_code(arg))
    elif op == "analyze":
        print(analyze(arg or None))
    else:
        print("unknown op: %s" % op)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

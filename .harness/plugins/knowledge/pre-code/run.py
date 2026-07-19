"""pre-code 플러그인 독립 CLI (standalone). adapter 없이도 동작.

  python run.py search "<질의>"
  python run.py update-code "<변경 요약>"
  python run.py analyze ["<루트>"]
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

import plugin


def main(argv):
    if not argv:
        print("usage: run.py search|update-code|analyze [args]")
        return 2
    op = argv[0]
    arg = " ".join(argv[1:])
    if op == "search":
        print(plugin.search(arg))
    elif op == "update-code":
        print(plugin.update_code(arg))
    elif op == "analyze":
        print(plugin.analyze(arg or None))
    else:
        print("unknown op: %s" % op)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

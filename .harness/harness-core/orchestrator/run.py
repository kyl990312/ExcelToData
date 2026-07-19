"""하네스 진입점.

사용:
  python harness-core/orchestrator/run.py start <item> [--k=v ...]
  python harness-core/orchestrator/run.py approve [item]
  python harness-core/orchestrator/run.py finalize [item]
  python harness-core/orchestrator/run.py advance [item]
  python harness-core/orchestrator/run.py status [item]

start/approve/finalize는 마커를 기록한 뒤 orchestrator를 전진시켜 다음 게이트에서 정지한다.
"""
import os
import sys
import json

# sys.path 부트스트랩: orchestrator/(top-level 모듈) + harness-core/(ports·adapters·guardrails 패키지)
_HERE = os.path.dirname(os.path.abspath(__file__))
_CORE = os.path.dirname(_HERE)
for _p in (_HERE, _CORE):
    if _p not in sys.path:
        sys.path.insert(0, _p)

# Windows 콘솔(cp949)에서도 유니코드 출력 안전하게
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

import context
import config as cfgmod
import markers
import engine


def _parse_params(args):
    d = {}
    for a in args:
        if a.startswith("--"):
            a = a[2:]
        if "=" in a:
            k, v = a.split("=", 1)
            lv = v.lower()
            if lv in ("true", "false"):
                v = lv == "true"
            d[k] = v
    return d


def main(argv):
    if not argv:
        print("usage: run.py start|approve|finalize|advance|status|auto [item|on|off] [--k=v]")
        return 2

    sub = argv[0]
    item = argv[1] if len(argv) > 1 and not argv[1].startswith("-") else None

    cfg = cfgmod.load()
    ds = context.make_doc_store(cfg)

    if sub == "start":
        if not item:
            print("start requires <item>")
            return 2
        markers.start(item, _parse_params(argv[2:]))
        res = engine.advance(item, cfg, ds)

    elif sub == "approve":
        item = item or markers.active_item()
        if not item:
            print("no active item (specify <item>)")
            return 2
        markers.approve(item, markers.get_phase(item))
        res = engine.advance(item, cfg, ds)

    elif sub == "finalize":
        item = item or markers.active_item()
        if not item:
            print("no active item (specify <item>)")
            return 2
        markers.finalize(item)
        res = engine.advance(item, cfg, ds)

    elif sub == "advance":
        item = item or markers.active_item()
        res = engine.advance(item, cfg, ds)

    elif sub == "status":
        item = item or markers.active_item()
        res = engine.status(item)

    elif sub == "auto":
        flag = cfgmod.auto_flag_path()
        if item == "on":
            open(flag, "w").close()
            print("auto-develop: ON  (모든 phase 게이트 스킵 — 끝까지 자동)")
        elif item == "off":
            if os.path.exists(flag):
                os.remove(flag)
            print("auto-develop: OFF (사람 게이트 켬 — 기본)")
        else:
            print("auto-develop: %s" % ("ON" if cfgmod.is_auto(cfg) else "OFF"))
        return 0

    else:
        print("unknown subcommand: %s" % sub)
        return 2

    print(json.dumps(res, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

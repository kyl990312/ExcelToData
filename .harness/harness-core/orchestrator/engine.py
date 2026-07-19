"""결정적 Phase 상태기계. 게이트마다 정지하고, 재호출되면 현재 Phase부터 이어간다.

전환 강제: 각 분기는 (선행 종료 사실) + (default 모드 게이트 마커)가 모두 있어야 다음 Phase로.
"""
import markers
import loop
import runners
import config as cfgmod


def advance(item, cfg, ds):
    if item is None:
        return _status(item, "no active item")
    phase = markers.get_phase(item)
    if phase is None:
        return _status(item, "no such item")

    auto = cfgmod.is_auto(cfg)  # auto 모드면 사람 게이트(approval/finalize)를 스킵

    while True:
        if markers.is_escalated(item):
            return _status(item, "escalated")

        if phase == "planning":
            if loop.run(item, "planning", cfg, ds) == "ESCALATED":
                return _status(item, "escalated")
            if not (auto or markers.is_approved(item, "planning")):
                return _gate(item, "planning")
            markers.set_phase(item, "dev-loop")
            phase = "dev-loop"

        elif phase == "dev-loop":
            if loop.run(item, "dev-loop", cfg, ds) == "ESCALATED":
                return _status(item, "escalated")
            if not (auto or markers.is_approved(item, "dev-loop")):
                return _gate(item, "dev-loop")
            markers.set_phase(item, "user-adjust")
            phase = "user-adjust"

        elif phase == "user-adjust":
            if not (auto or markers.is_finalized(item)):
                return _gate(item, "user-adjust (awaiting /finalize)")
            markers.set_phase(item, "finalizing")
            phase = "finalizing"

        elif phase == "finalizing":
            cr = markers.read_commit_result(item)
            docs = ds.exists("report/" + item)
            if cr and cr.get("committed") and docs:
                committed = True
            else:
                committed, docs = runners.run_finalize(item, cfg, ds)
            if not (committed and docs):
                return _status(item, "finalizing incomplete (committed=%s docs=%s)" % (committed, docs))
            if not (auto or markers.is_approved(item, "finalizing")):
                return _gate(item, "finalizing")
            markers.mark_done(item)
            return _status(item, "done")

        elif phase == "done":
            return _status(item, "done")

        else:
            return _status(item, "unknown phase: %s" % phase)


def status(item):
    if item is None:
        return {"error": "no active item"}
    return {
        "item": item,
        "phase": markers.get_phase(item),
        "signals": {
            "plan_approved": _approved(markers.read_verdict(item, "plan")),
            "review_approved": _approved(markers.read_verdict(item, "review")),
            "test_passed": bool((markers.read_test_result(item) or {}).get("passed")),
            "committed": bool((markers.read_commit_result(item) or {}).get("committed")),
            "approvals": {ph: markers.is_approved(item, ph) for ph in ("planning", "dev-loop", "finalizing")},
            "finalized": markers.is_finalized(item),
            "done": markers.is_done(item),
            "escalated": markers.is_escalated(item),
        },
    }


def _approved(v):
    return bool(v and v.get("approved"))


def _gate(item, gate):
    return {"item": item, "phase": markers.get_phase(item), "status": "AWAITING_APPROVAL", "gate": gate}


def _status(item, msg):
    return {"item": item, "phase": markers.get_phase(item) if item else None, "status": msg}

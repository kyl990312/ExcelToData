"""공유 생성⟷리뷰 루프 (planning · dev-loop 공통 메커니즘 — 명세 §3.3).

반환: "OK" (Phase 종료 사실 충족) | "ESCALATED" (캡 초과 또는 산출물 파손).
재진입 안전: 이미 종료 사실이 서 있으면 즉시 OK (작업 재실행 안 함 → 결정성).
"""
import guardrails.caps as caps
import markers
import stages
import runners


def _end_fact(item, phase):
    if phase == "planning":
        v = markers.read_verdict(item, "plan")
        return bool(v and v.get("approved"))
    # dev-loop
    v = markers.read_verdict(item, "review")
    t = markers.read_test_result(item)
    return bool(v and v.get("approved") and t and t.get("passed"))


def run(item, phase, cfg, ds):
    if _end_fact(item, phase):
        return "OK"

    while not caps.exceeded(item, phase, cfg):
        markers.bump_counter(item, phase)

        if phase == "planning":
            stages.run(item, "plan", cfg, ds)
            if not ds.exists("plan/" + item):
                caps.escalate(item, phase, "plan artifact missing")
                return "ESCALATED"
            stages.run(item, "plan-review", cfg, ds)
            v = markers.read_verdict(item, "plan")
            if v is None:
                caps.escalate(item, phase, "plan verdict missing/invalid")
                return "ESCALATED"
            if v.get("approved"):
                return "OK"

        else:  # dev-loop
            stages.run(item, "dev", cfg, ds)
            stages.run(item, "review", cfg, ds)
            v = markers.read_verdict(item, "review")
            if v is None:
                caps.escalate(item, phase, "review verdict missing/invalid")
                return "ESCALATED"
            if v.get("approved"):
                if runners.build_test(item, cfg):
                    return "OK"
                # 빌드/테스트 실패 → 같은 캡 안에서 dev 재진입 (while 계속)

    caps.escalate(item, phase, "cap exceeded (%d)" % caps.cap_for(cfg, phase))
    return "ESCALATED"

"""반복 캡 + 에스컬레이션. loop가 Phase별 시도 횟수를 여기로 통제한다."""
import markers


def cap_for(cfg, phase):
    return (cfg.get("budgets", {}).get(phase, {}) or {}).get("max_iterations", 3)


def exceeded(item, phase, cfg):
    return markers.counter(item, phase) >= cap_for(cfg, phase)


def escalate(item, phase, reason):
    markers.mark_escalated(item, phase, reason)

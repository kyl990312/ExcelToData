"""task/ 마커 read/write — 전환 진실의 유일한 소유자.

다른 모듈은 task/를 직접 만지지 않고 여기를 경유한다.
(예외: AI stages는 계약상 verdict/commit_msg 파일을 직접 쓴다 — 그건 산출물이고,
 코드는 그 파일을 '검증된 사실'로만 읽는다.)
"""
import os
import json
import time
import context


def _item_dir(item):
    return os.path.join(context.TASK_DIR, item)


def _ensure(d):
    os.makedirs(d, exist_ok=True)


def _now():
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def _read_json(path):
    if not os.path.exists(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None


def _write_json(path, obj):
    _ensure(os.path.dirname(path))
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=2)


# --- queue / phase ---

def start(item, params=None):
    _ensure(os.path.join(context.TASK_DIR, "queue"))
    _ensure(_item_dir(item))
    _write_json(
        os.path.join(context.TASK_DIR, "queue", item + ".json"),
        {"item_id": item, "params": params or {}, "ts": _now()},
    )
    set_phase(item, "planning")


def read_params(item):
    data = _read_json(os.path.join(context.TASK_DIR, "queue", item + ".json"))
    return (data or {}).get("params", {})


def get_phase(item):
    p = os.path.join(_item_dir(item), "phase")
    if not os.path.exists(p):
        return None
    with open(p, "r", encoding="utf-8") as f:
        return f.read().strip()


def set_phase(item, phase):
    _ensure(_item_dir(item))
    with open(os.path.join(_item_dir(item), "phase"), "w", encoding="utf-8") as f:
        f.write(phase)


# --- verdicts (plan / review) ---

def read_verdict(item, kind):
    return _read_json(os.path.join(_item_dir(item), kind + ".verdict.json"))


def write_verdict(item, kind, approved, notes=""):
    _write_json(
        os.path.join(_item_dir(item), kind + ".verdict.json"),
        {"approved": bool(approved), "notes": notes},
    )


# --- build/test result ---

def read_test_result(item):
    return _read_json(os.path.join(_item_dir(item), "test.result.json"))


def write_test_result(item, passed, exit_code):
    _write_json(
        os.path.join(_item_dir(item), "test.result.json"),
        {"passed": bool(passed), "exit_code": int(exit_code)},
    )


# --- commit ---

def read_commit_msg(item):
    p = os.path.join(_item_dir(item), "commit_msg.txt")
    if not os.path.exists(p):
        return None
    with open(p, "r", encoding="utf-8") as f:
        return f.read().strip()


def write_commit_msg(item, msg):
    _ensure(_item_dir(item))
    with open(os.path.join(_item_dir(item), "commit_msg.txt"), "w", encoding="utf-8") as f:
        f.write(msg)


def read_commit_result(item):
    return _read_json(os.path.join(_item_dir(item), "commit.result.json"))


def write_commit_result(item, committed, exit_code):
    _write_json(
        os.path.join(_item_dir(item), "commit.result.json"),
        {"committed": bool(committed), "exit_code": int(exit_code)},
    )


# --- approvals / finalize ---

def approve(item, phase):
    d = os.path.join(_item_dir(item), "approvals")
    _ensure(d)
    open(os.path.join(d, phase), "w").close()


def is_approved(item, phase):
    return os.path.exists(os.path.join(_item_dir(item), "approvals", phase))


def finalize(item):
    _ensure(_item_dir(item))
    open(os.path.join(_item_dir(item), "human_approved"), "w").close()


def is_finalized(item):
    return os.path.exists(os.path.join(_item_dir(item), "human_approved"))


# --- counters (캡 합산) ---

def counter(item, phase):
    data = _read_json(os.path.join(_item_dir(item), "counters.json")) or {}
    return data.get(phase, 0)


def bump_counter(item, phase):
    p = os.path.join(_item_dir(item), "counters.json")
    data = _read_json(p) or {}
    data[phase] = data.get(phase, 0) + 1
    _write_json(p, data)
    return data[phase]


# --- terminal states ---

def mark_done(item):
    open(os.path.join(_item_dir(item), "done"), "w").close()
    set_phase(item, "done")


def is_done(item):
    return os.path.exists(os.path.join(_item_dir(item), "done"))


def mark_escalated(item, phase, reason):
    _ensure(_item_dir(item))
    open(os.path.join(_item_dir(item), "escalated"), "w").close()
    _write_json(
        os.path.join(_item_dir(item), "escalated.json"),
        {"phase": phase, "reason": reason},
    )


def is_escalated(item):
    return os.path.exists(os.path.join(_item_dir(item), "escalated"))


def active_item():
    """done/escalated가 아닌 단일 아이템 반환 (없거나 모호하면 None)."""
    qd = os.path.join(context.TASK_DIR, "queue")
    if not os.path.isdir(qd):
        return None
    items = [f[:-5] for f in os.listdir(qd) if f.endswith(".json")]
    live = [it for it in items if not is_done(it) and not is_escalated(it)]
    return live[0] if len(live) == 1 else None

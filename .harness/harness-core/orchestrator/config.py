"""harness config 로드 (JSON, 표준 라이브러리만)."""
import os
import json

import context

DEFAULTS = {
    "doc_backend": "local",
    "status_backend": "none",
    "mode": "default",
    "default_model": "claude-sonnet-4-6",
    "models": {},
    "stage_tools": {},
    "stub_stages": True,
    "build_cmd": "",
    "test_cmd": "",
    "commit_cmd": "",
    "commit_repo_dir": ".",
    "budgets": {"planning": {"max_iterations": 3}, "dev-loop": {"max_iterations": 5}},
    "schema_version": 1,
}


def load():
    data = {}
    if os.path.exists(context.CONFIG_PATH):
        try:
            with open(context.CONFIG_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
        except Exception:
            data = {}
    return _merge(DEFAULTS, data)


def _merge(defaults, data):
    out = dict(defaults)
    for k, v in (data or {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k] = _merge(out[k], v)
        else:
            out[k] = v
    return out


def model_for(cfg, task):
    return (cfg.get("models") or {}).get(task) or cfg.get("default_model")


def auto_flag_path():
    return os.path.join(context.HARNESS_ROOT, "config", ".auto")


def is_auto(cfg):
    """auto 모드 여부 = config.mode == auto  또는  토글 플래그(config/.auto) 존재."""
    if (cfg.get("mode") or "default") == "auto":
        return True
    return os.path.exists(auto_flag_path())

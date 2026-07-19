"""pre-code 설정/경로. 워크스페이스(데이터)는 config가 가리키며 harness root 기준으로 해석한다."""
import os
import json

CORE_DIR = os.path.dirname(os.path.abspath(__file__))     # .../plugins/knowledge/pre-code/core
PKG_DIR = os.path.dirname(CORE_DIR)                        # .../plugins/knowledge/pre-code
CONFIG_PATH = os.path.join(PKG_DIR, "config.json")

# harness root = 플러그인 위치에서 3단계 위 (pre-code → knowledge → plugins → harness root).
# 작업본=`.harness/developer`, 배포본=`.harness`. cwd와 무관하게 결정적.
HARNESS_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(PKG_DIR)))

DEFAULTS = {"model": "claude-sonnet-4-6", "stub": True, "workspace": "knowledges/pre-code", "budgets": {"review": 3}}


def load_config():
    cfg = dict(DEFAULTS)
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                cfg.update(json.load(f) or {})
        except Exception:
            pass
    return cfg


def workspace_dir(cfg):
    """워크스페이스 = config.workspace. 상대경로는 **harness root 기준**(cwd 무관). 절대경로는 그대로."""
    ws = cfg.get("workspace") or "knowledges/pre-code"
    return ws if os.path.isabs(ws) else os.path.normpath(os.path.join(HARNESS_ROOT, ws))

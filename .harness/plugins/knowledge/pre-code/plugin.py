"""pre-code knowledge 플러그인 — 표준 인터페이스. adapter가 로드해 호출한다.

표준 IF: search(query) / update_code(changes) / analyze(root). 데이터는 core/ctx의 workspace.
"""
import os
import sys

_CORE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "core")
if _CORE not in sys.path:
    sys.path.insert(0, _CORE)

import ctx as _ctx
import ops as _ops

NAME = "pre-code"
CAPABILITIES = ["search", "update_code", "analyze"]


def search(query):
    return _ops.search(_ctx.load_config(), query)


def update_code(changes):
    return _ops.update(_ctx.load_config(), changes)


def analyze(root=None):
    return _ops.analyze(_ctx.load_config(), root)

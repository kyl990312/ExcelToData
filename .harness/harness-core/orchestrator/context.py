"""공유 경로 및 팩토리. 모든 모듈이 여기서 절대 경로를 얻는다 (하드코딩 금지)."""
import os

# .harness/developer 루트 = 이 파일 기준 3단계 위 (orchestrator/ -> harness-core/ -> developer/)
HARNESS_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HARNESS_CORE = os.path.join(HARNESS_ROOT, "harness-core")

TASK_DIR = os.path.join(HARNESS_ROOT, "task")
DOCS_DIR = os.path.join(HARNESS_ROOT, "docs")
KNOWLEDGE_DIR = os.path.join(HARNESS_ROOT, "knowledge")
CONFIG_PATH = os.path.join(HARNESS_ROOT, "config", "harness.json")
STAGES_DIR = os.path.join(HARNESS_CORE, "stages")
SCRIPTS_DIR = os.path.join(HARNESS_CORE, "scripts")

# Phase 순서 (참조용)
PHASES = ["planning", "dev-loop", "user-adjust", "finalizing", "done"]
GATED_PHASES = ["planning", "dev-loop", "finalizing"]  # /approve 게이트가 붙는 Phase


def make_doc_store(cfg):
    """config.doc_backend에 맞는 DocStore 어댑터를 반환 (본체는 포트만 의존)."""
    backend = (cfg or {}).get("doc_backend", "local")
    if backend == "local":
        import adapters.local as local
        return local.LocalDocStore(DOCS_DIR)
    raise ValueError("unknown doc_backend: %s" % backend)

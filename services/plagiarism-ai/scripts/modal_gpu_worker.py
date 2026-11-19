"""Deploy the UGTM GPU inference contract on Modal; see docs/modal-gpu-setup.md."""
from pathlib import Path

import modal


def download_models():
    from huggingface_hub import snapshot_download
    for model in ("BAAI/bge-m3", "BAAI/bge-reranker-v2-m3"):
        snapshot_download(model, revision="main")


image = (
    modal.Image.debian_slim(python_version="3.12")
    .apt_install("git")
    .pip_install(
        "torch>=2.6.0,<3", "safetensors>=0.4.5,<1", "FlagEmbedding>=1.3.4,<2",
        "transformers>=4.48,<5", "sentence-transformers>=3.3,<4", "huggingface-hub>=0.26,<1",
        "datasets>=2.19,<3.2", "numpy>=1.26,<3", "fastapi>=0.115,<1", "pydantic>=2.7,<3",
    )
    .run_function(download_models, timeout=1800)
    .add_local_file(Path(__file__).with_name("modal_gpu_api.py"), "/root/modal_gpu_api.py")
)
app = modal.App("ugtm-gpu-worker")


@app.function(
    image=image, gpu="T4", secrets=[modal.Secret.from_name("ugtm-gpu-worker")],
    min_containers=0, max_containers=1, scaledown_window=60, timeout=600, startup_timeout=600,
)
@modal.asgi_app()
def gpu_api():
    import os
    import sys
    import torch
    from FlagEmbedding import BGEM3FlagModel, FlagReranker

    sys.path.insert(0, "/root")
    from modal_gpu_api import create_app

    key = os.environ.get("GPU_WORKER_API_KEY", "").strip()
    if not key:
        raise ValueError("GPU_WORKER_API_KEY is required")
    if not torch.cuda.is_available():
        raise RuntimeError("No CUDA GPU is available")
    embedder = BGEM3FlagModel("BAAI/bge-m3", use_fp16=True, devices=["cuda:0"])
    reranker = FlagReranker("BAAI/bge-reranker-v2-m3", use_fp16=True, devices=["cuda:0"])
    return create_app(embedder, reranker, key, lambda: {
        "device": torch.cuda.get_device_name(0), "cuda": True,
        "vram_allocated_mb": round(torch.cuda.memory_allocated(0) / (1024 ** 2), 2),
    })

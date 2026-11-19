from pathlib import Path
from typing import Literal

from pydantic_settings import BaseSettings, SettingsConfigDict

PROJECT_ROOT = Path(__file__).resolve().parent.parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    postgres_dsn: str
    postgres_direct_dsn: str | None = None
    redis_url: str
    api_key: str
    max_upload_mb: int = 20
    data_dir: Path = PROJECT_ROOT / "data" / "runtime"
    embedding_model: str = "BAAI/bge-m3"
    embedding_revision: str | None = None
    embedding_dim: int = 1024
    reranker_model: str = "BAAI/bge-reranker-v2-m3"
    reranker_revision: str | None = None
    classifier_enabled: bool = False
    classifier_path: Path = (
        PROJECT_ROOT / "artifacts" / "suspicious_passage_classifier.joblib"
    )
    inference_mode: Literal["remote", "local"] = "remote"
    remote_gpu_url: str | None = None
    remote_gpu_api_key: str | None = None
    remote_gpu_timeout_seconds: float = 180.0
    remote_gpu_health_timeout_seconds: float = 5.0
    remote_gpu_batch_size: int = 32
    celery_queue: str = "celery"

    # Storage Backend Configuration (CP-01.4)
    storage_backend: Literal["local", "r2", "s3"] = "local"
    s3_endpoint_url: str | None = None
    s3_access_key_id: str | None = None
    s3_secret_access_key: str | None = None
    s3_bucket_name: str | None = None
    s3_region_name: str = "auto"
    s3_presigned_url_expire_seconds: int = 3600
    r2_endpoint_url: str | None = None
    r2_account_id: str | None = None
    r2_access_key_id: str | None = None
    r2_secret_access_key: str | None = None
    r2_bucket_name: str | None = None

    @property
    def effective_storage_backend(self) -> str:
        backend = (self.storage_backend or "").strip().lower()
        if backend in ("r2", "s3"):
            return "s3"
        return "local"

    @property
    def effective_s3_endpoint(self) -> str | None:
        if self.s3_endpoint_url:
            return self.s3_endpoint_url.strip()
        if self.r2_endpoint_url:
            return self.r2_endpoint_url.strip()
        if self.r2_account_id:
            return f"https://{self.r2_account_id.strip()}.r2.cloudflarestorage.com"
        return None

    @property
    def effective_s3_access_key(self) -> str | None:
        return (self.s3_access_key_id or self.r2_access_key_id or "").strip() or None

    @property
    def effective_s3_secret_key(self) -> str | None:
        return (
            (self.s3_secret_access_key or self.r2_secret_access_key or "").strip()
            or None
        )

    @property
    def effective_s3_bucket(self) -> str:
        return (
            (self.s3_bucket_name or self.r2_bucket_name or "ugtm-ai-dev").strip()
        )


settings = Settings()

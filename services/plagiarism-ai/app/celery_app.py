from __future__ import annotations

import logging
from celery import Celery

from app.config import settings

logger = logging.getLogger(__name__)

# Persistent reports, job statuses, and error codes are stored in Neon PostgreSQL (integrity.jobs).
# Redis acts strictly as a message broker for task orchestration.
celery_app = Celery(
    "ugtm_plagiarism_ai",
    broker=settings.redis_url,
    backend=None,
    include=["app.tasks"],
)

celery_app.conf.update(
    task_default_queue=settings.celery_queue,
    task_serializer="json",
    accept_content=["json"],
    result_serializer="json",
    timezone="Asia/Ho_Chi_Minh",
    enable_utc=True,
    task_track_started=True,
    broker_connection_retry_on_startup=True,
    broker_connection_max_retries=10,
    worker_prefetch_multiplier=1,
    task_acks_late=True,
    task_reject_on_worker_lost=True,
    broker_transport_options={
        "visibility_timeout": 3600,  # 1 hour before message is redelivered
        "socket_timeout": 15,
        "socket_connect_timeout": 15,
        "retry_on_timeout": True,
        "max_connections": 10,
    },
)

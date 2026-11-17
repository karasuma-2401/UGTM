"""Read-only preflight for the AI bucket and broker (never wakes the GPU)."""
import logging
import time

import redis

from app.config import settings
from app.storage import get_storage


def main(attempts: int = 10, delay: float = 2) -> int:
    for attempt in range(attempts):
        try:
            redis.Redis.from_url(settings.redis_url, socket_connect_timeout=5, socket_timeout=5).ping()
            storage = get_storage()
            storage.client.head_bucket(Bucket=storage.bucket)
            return 0
        except Exception:
            if attempt + 1 < attempts:
                time.sleep(delay)
    logging.error("AI broker/bucket preflight failed; verify services, bucket and scoped credentials")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

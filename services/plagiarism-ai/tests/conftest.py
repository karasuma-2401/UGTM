import os
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from dotenv import dotenv_values

env_path = PROJECT_ROOT / ".env"
if env_path.is_file():
    for key, val in dotenv_values(env_path).items():
        if val and key not in os.environ:
            os.environ[key] = val

os.environ.setdefault("POSTGRES_DSN", "postgresql://ugtm_ai:test@localhost:5432/ugtm_ai")
os.environ.setdefault("REDIS_URL", "redis://localhost:6379/0")
os.environ.setdefault("API_KEY", "test-api-key")

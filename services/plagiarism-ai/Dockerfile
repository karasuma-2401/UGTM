FROM python:3.12-slim

WORKDIR /service

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

RUN apt-get update \
    # Refresh zlib from Debian security updates even when the base image is cached.
    && apt-get install -y --no-install-recommends libgomp1 zlib1g \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
COPY requirements ./requirements
RUN python -m pip install --no-cache-dir -r requirements/cpu-worker.txt

COPY app ./app
COPY configs ./configs
COPY scripts ./scripts
COPY sql ./sql
COPY training ./training
# Optional classifier artifacts are provisioned separately, never copied from a developer machine.
RUN useradd --uid 10001 --create-home ai \
    && mkdir -p /data ./artifacts \
    && chown -R ai:ai /data ./artifacts
USER ai
ENV DATA_DIR=/data

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]

#!/usr/bin/env bash
set -euo pipefail
umask 077

# Invoked from an immutable release directory copied by GitHub Actions.
RELEASE_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
APP_DIR="${UGTM_DEPLOY_ROOT:-$HOME/ugtm/production}"
COMPOSE_FILE="$RELEASE_DIR/docker/docker-compose.prod.yml"
ENV_FILE="$RELEASE_DIR/runtime.env"

required=(SOURCE_SHA IMAGE_OWNER API_HOST ACME_EMAIL APP_URL DATABASE_CONNECTION_STRING
  AI_POSTGRES_DSN AI_POSTGRES_DIRECT_DSN PLAGIARISM_AI_API_KEY PLAGIARISM_AI_DEFAULT_CORPUS
  REMOTE_GPU_URL REMOTE_GPU_API_KEY MINIO_ROOT_USER MINIO_ROOT_PASSWORD
  S3_ACCESS_KEY S3_SECRET_KEY AI_S3_ACCESS_KEY AI_S3_SECRET_KEY
  JWT_PRIVATE_KEY_PEM_BASE64 IDENTITY_IP_HASH_KEY SMTP_HOST SMTP_FROM_ADDRESS DEPLOY_GITHUB_TOKEN)
for name in "${required[@]}"; do
  if [[ -z "${!name:-}" ]]; then echo "Missing deployment setting: $name" >&2; exit 1; fi
done
[[ "$SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]] || { echo 'SOURCE_SHA must be a full commit SHA.' >&2; exit 1; }
[[ "$API_HOST" =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]*[a-zA-Z0-9]$ ]] || { echo 'Invalid API_HOST.' >&2; exit 1; }
[[ "$APP_URL" == https://* && ${#IDENTITY_IP_HASH_KEY} -ge 32 && ${#PLAGIARISM_AI_API_KEY} -ge 32 ]] || {
  echo 'Use an HTTPS frontend origin and keys of at least 32 characters.' >&2; exit 1;
}
command -v setfacl >/dev/null || { echo 'Install acl (setfacl) before deploying.' >&2; exit 1; }
docker compose version >/dev/null
if [[ -e "$APP_DIR/current" && ! -L "$APP_DIR/current" ]]; then
  echo 'The current release path is not a symlink; inspect it before deploying.' >&2
  exit 1
fi
if [[ -f "$HOME/ugtm/secrets/ugtm-jwt-private.pem" && -z "${DATA_PROTECTION_VOLUME:-}" ]]; then
  echo 'An older installation exists. Configure DATA_PROTECTION_VOLUME from the inspected old stack.' >&2
  exit 1
fi

mkdir -p "$RELEASE_DIR/secrets" "$APP_DIR/ai-artifacts"
chmod 755 "$APP_DIR/ai-artifacts"
export IMAGE_TAG="sha-$SOURCE_SHA"
export JWT_PRIVATE_KEY_FILE="$RELEASE_DIR/secrets/ugtm-jwt-private.pem"
export AI_ARTIFACTS_DIR="$APP_DIR/ai-artifacts"

# Quote dotenv values and escape interpolation; never evaluate a secret as shell code.
# Keep a private settings file per release so rollback does not borrow new settings.
settings=(IMAGE_OWNER IMAGE_TAG API_HOST ACME_EMAIL APP_URL DATABASE_CONNECTION_STRING
  AI_POSTGRES_DSN AI_POSTGRES_DIRECT_DSN PLAGIARISM_AI_API_KEY PLAGIARISM_AI_DEFAULT_CORPUS
  REMOTE_GPU_URL REMOTE_GPU_API_KEY MINIO_ROOT_USER MINIO_ROOT_PASSWORD
  S3_ACCESS_KEY S3_SECRET_KEY S3_BUCKET_NAME AI_S3_ACCESS_KEY AI_S3_SECRET_KEY AI_S3_BUCKET_NAME
  MINIO_IMAGE MINIO_MC_IMAGE JWT_PRIVATE_KEY_FILE AI_ARTIFACTS_DIR AI_CLASSIFIER_ENABLED
  IDENTITY_IP_HASH_KEY SMTP_HOST SMTP_PORT SMTP_FROM_ADDRESS SMTP_FROM_NAME SMTP_USERNAME
  SMTP_PASSWORD SMTP_SECURITY SMTP_TIMEOUT_SECONDS DATA_PROTECTION_VOLUME CADDY_DATA_VOLUME
  CADDY_CONFIG_VOLUME MINIO_DATA_VOLUME)
ENV_TMP=$(mktemp "$RELEASE_DIR/runtime.env.XXXXXXXX")
trap 'rm -f "$ENV_TMP"' EXIT
for name in "${settings[@]}"; do
  value=${!name:-}
  [[ "$value" != *$'\n'* && "$value" != *$'\r'* ]] || { echo "Multiline setting is unsupported: $name" >&2; exit 1; }
  value=${value//\\/\\\\}
  value=${value//\"/\\\"}
  value=${value//\$/\$\$}
  value=${value//$'\t'/\\t}
  printf '%s="%s"\n' "$name" "$value" >> "$ENV_TMP"
done
mv "$ENV_TMP" "$ENV_FILE"
trap - EXIT

compose() { docker compose -p ugtm-production --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"; }
compose config --quiet
printf '%s' "$DEPLOY_GITHUB_TOKEN" | docker login ghcr.io -u "$IMAGE_OWNER" --password-stdin
compose pull

API_IMAGE="ghcr.io/$IMAGE_OWNER/ugtm-api:$IMAGE_TAG"
RUNTIME_UID=$(docker run --rm --entrypoint id "$API_IMAGE" -u)
[[ "$RUNTIME_UID" =~ ^[0-9]+$ && "$RUNTIME_UID" != 0 ]] || { echo 'The API must run as a non-root UID.' >&2; exit 1; }
KEY_TMP=$(mktemp "$RELEASE_DIR/secrets/jwt-key.XXXXXXXX")
trap 'rm -f "$KEY_TMP"' EXIT
printf '%s' "$JWT_PRIVATE_KEY_PEM_BASE64" | base64 --decode > "$KEY_TMP"
setfacl -m "u:$RUNTIME_UID:r" "$KEY_TMP"
docker run --rm --entrypoint /bin/sh -v "$KEY_TMP:/run/secrets/ugtm-jwt-private.pem:ro" \
  "$API_IMAGE" -c 'test -r /run/secrets/ugtm-jwt-private.pem'
mv -f "$KEY_TMP" "$JWT_PRIVATE_KEY_FILE"
trap - EXIT

case "${DEPLOY_MODE:-deploy}" in
  prepare)
    echo "Prepared release $SOURCE_SHA without starting or stopping services. Follow docs/production-deployment.md."
    exit 0
    ;;
  deploy) ;;
  *) echo 'DEPLOY_MODE must be prepare or deploy.' >&2; exit 1;;
esac

# Never automatically stop an older stack or replace its key-ring volume.
for port in 80 443 8443; do
  foreign=$(docker ps --filter "publish=$port" --format '{{.Names}}' | grep -v '^ugtm-production-gateway$' || true)
  if [[ -n "$foreign" ]]; then
    echo "Port $port belongs to an older/unrelated stack; complete the documented volume cutover first." >&2
    exit 1
  fi
done

# Schema verification is read-only. Apply reviewed migrations explicitly after backup.
compose run --rm --no-deps ai-api python -m scripts.check_schema
if [[ "${AI_CLASSIFIER_ENABLED:-false}" == true ]]; then
  compose run --rm --no-deps ai-worker python -c 'from app.classifier import load_classifier; load_classifier()'
fi
compose up -d minio ai-redis
# Fail visibly if bucket/user provisioning or copying existing objects is incomplete.
compose run --rm --no-deps ai-api python -m scripts.check_storage

compose up -d --wait --wait-timeout 180 --remove-orphans
compose exec -T ai-worker celery -A app.celery_app.celery_app inspect ping --timeout=10
curl --fail --silent --show-error --retry 12 --retry-delay 5 --retry-all-errors \
  "https://${API_HOST}/api/modules/Identity/ping"
curl --fail --silent --show-error --retry 12 --retry-delay 5 --retry-all-errors \
  "https://${API_HOST}:8443/minio/health/live"

# Preserve the previous release for an explicit, schema-compatible rollback.
if [[ -L "$APP_DIR/current" ]]; then
  previous=$(readlink "$APP_DIR/current")
  if [[ "$previous" != "$RELEASE_DIR" ]]; then ln -sfn "$previous" "$APP_DIR/previous"; fi
fi
ln -sfn "$RELEASE_DIR" "$APP_DIR/current"
echo "Deployed production release $SOURCE_SHA. Run the documented user-flow smoke checks."

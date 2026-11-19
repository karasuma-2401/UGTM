#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

RELEASE_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
APP_DIR="${UGTM_DEPLOY_ROOT:-$HOME/ugtm/production}"
MODE=${1:-candidate}
mkdir -p "$APP_DIR" "$APP_DIR/ai-artifacts"
for tool in python3 flock timeout setfacl curl docker; do command -v "$tool" >/dev/null; done
exec 9>"$APP_DIR/deploy.lock"
flock -n 9 || { echo 'Another production operation holds the VM lock.' >&2; exit 1; }
meta() { python3 "$RELEASE_DIR/scripts/production_metadata.py" "$RELEASE_DIR" "$@"; }
compose_at() { local dir=$1; shift; docker compose -p ugtm-production --env-file "$dir/runtime.env" -f "$dir/docker/docker-compose.prod.yml" "$@"; }
compose() { compose_at "$RELEASE_DIR" "$@"; }
state() { meta state "$1"; }
worker_node() {
  local dir=$1 container hostname
  container=$(compose_at "$dir" ps -q ai-worker) || return 1
  [[ -n "$container" ]] || return 0
  [[ "$(docker inspect --format '{{.State.Running}}' "$container")" == true ]] || return 0
  hostname=$(docker inspect --format '{{.Config.Hostname}}' "$container") || return 1
  [[ "$hostname" =~ ^[A-Za-z0-9_.-]+$ ]] || return 1
  printf 'celery@%s\n' "$hostname"
}
drain_worker() {
  local node
  node=$(worker_node "$1") || return 1
  [[ -n "$node" ]] || return 0
  compose run --rm --no-deps ai-api python -m scripts.production_ops drain --worker "$node" --timeout 300
}
check_artifacts() {
  local dir=$1
  if [[ "$(python3 "$dir/scripts/production_metadata.py" "$dir" get classifier_enabled)" == true ]]; then
    compose_at "$dir" run --rm --no-deps ai-worker python -m scripts.production_ops classifier-fingerprint \
      | python3 "$dir/scripts/production_metadata.py" "$dir" check-artifacts || return 1
  fi
}
current=$(readlink -f "$APP_DIR/current" || true)
validate_path() { [[ "$1" == "$APP_DIR/releases/"* && -f "$1/runtime.env" ]]; }
if [[ -f "$APP_DIR/pending" && "$(cat "$APP_DIR/pending")" != "$RELEASE_DIR" ]]; then
  echo 'An unfinished deployment exists. Inspect pending/status and recover it first.' >&2; exit 1
fi
local_health() {
  local dir=$1 host
  host=$(python3 "$dir/scripts/production_metadata.py" "$dir" get api_host)
  for path in "443/api/modules/Identity/ping" "8443/minio/health/live"; do
    local port=${path%%/*} endpoint=/${path#*/}
    curl --fail --silent --show-error --output /dev/null --connect-timeout 3 --max-time 10 \
      --retry 3 --retry-delay 2 --retry-all-errors --retry-max-time 40 \
      --resolve "$host:$port:127.0.0.1" "https://$host:$port$endpoint" || return 1
  done
}
resume_old_worker() {
  local node
  if validate_path "$current"; then
    node=$(worker_node "$current") || return 1
    [[ -n "$node" ]] || return 0
    timeout 20 docker compose -p ugtm-production --env-file "$current/runtime.env" -f "$current/docker/docker-compose.prod.yml" \
      exec -T ai-worker celery -A app.celery_app.celery_app control add_consumer ugtm-ai-production --destination "$node" --timeout=5 >/dev/null || return 1
  fi
}
rollback() {
  local old
  old=$(cat "$RELEASE_DIR/previous-path" 2>/dev/null || true)
  validate_path "$old" || { state rollback_unavailable; return 1; }
  [[ -f "$old/release.json" ]] || { state legacy_rollback_requires_operator; return 1; }
  check_artifacts "$old" || { state artifact_rollback_requires_operator; return 1; }
  meta compatible "$old" || { state schema_rollback_requires_operator; return 1; }
  state checking_rollback
  compose_at "$old" run --rm --no-deps backend --check-schema || return 1
  compose_at "$old" run --rm --no-deps ai-api python -m scripts.check_schema || return 1
  if [[ "$(python3 "$old/scripts/production_metadata.py" "$old" get classifier_enabled)" == true ]]; then
    compose_at "$old" run --rm --no-deps ai-worker python -c 'from app.classifier import load_classifier; load_classifier()' || return 1
  fi
  # Warm shutdown never force-kills an in-flight scan during rollback.
  drain_worker "$RELEASE_DIR" || return 1
  compose_at "$old" up -d --wait --wait-timeout 180 --remove-orphans || return 1
  local_health "$old" || return 1
  current=$old
  resume_old_worker || return 1
  ln -sfn "$old" "$APP_DIR/current.next"
  mv -Tf "$APP_DIR/current.next" "$APP_DIR/current"
  rm -f "$APP_DIR/pending" "$RELEASE_DIR/mutated"
  state rolled_back
}
failure() {
  local code=$?
  [[ "$code" -ne 0 ]] || return 0
  trap - EXIT INT TERM HUP
  state failed || true
  # Status/exit codes only: application logs can contain personal data and signed URLs.
  compose ps --format json > "$RELEASE_DIR/containers.json" 2>/dev/null || true
  if [[ -f "$RELEASE_DIR/mutated" ]]; then
    rollback || state recovery_required
  else
    resume_old_worker || true
    rm -f "$APP_DIR/pending"
  fi
  exit "$code"
}
if [[ "$MODE" == prepare || "$MODE" == candidate ]]; then
  [[ ! -f "$RELEASE_DIR/status.json" ]] || { echo 'Use a fresh deployment attempt directory.' >&2; exit 1; }
fi
if [[ "$MODE" == prepare || "$MODE" == candidate || "$MODE" == finalize ]]; then
  # EXIT covers explicit guard exits as well as failing commands and terminated SSH sessions.
  trap failure EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM
  trap 'exit 129' HUP
fi

case "$MODE" in
  prepare|candidate)
    [[ "$RELEASE_DIR" == "$APP_DIR/releases/"* ]]
    python3 "$RELEASE_DIR/scripts/release.py" verify "$RELEASE_DIR"
    meta init
    state preparing
    # Keep the rollback baseline before any mutation, including the first migration to this workflow.
    printf '%s\n' "$current" > "$RELEASE_DIR/previous-path"
    available_kb=$(df -Pk "$APP_DIR" | awk 'NR==2 {print $4}')
    [[ "$available_kb" -ge "${MIN_FREE_DISK_KB:-2097152}" ]] || { echo 'At least 2 GiB free disk is required.' >&2; exit 1; }
    [[ -z "$current" || -f "$current/runtime.env" ]]
    compose config --quiet
    state pulling
    compose pull --quiet
    api_image=$(meta get api_image)
    uid=$(docker run --rm --entrypoint id "$api_image" -u)
    [[ "$uid" =~ ^[0-9]+$ && "$uid" != 0 ]]
    setfacl -m "u:$uid:r" "$RELEASE_DIR/secrets/jwt.pem"
    chmod 755 "$APP_DIR/ai-artifacts"
    if [[ "$(meta get classifier_enabled)" == true ]]; then
      compose run --rm --no-deps ai-worker python -m scripts.production_ops classifier-fingerprint | meta record-artifacts
    fi
    [[ "$MODE" != prepare ]] || { state prepared; exit 0; }
    printf '%s\n' "$RELEASE_DIR" > "$APP_DIR/pending"
    state preflight
    for port in 80 443 8443; do
      foreign=$(docker ps --filter "publish=$port" --format '{{.Names}}' | grep -v '^ugtm-production-gateway$' || true)
      [[ -z "$foreign" ]] || { echo "Port $port is held by an unrelated container." >&2; exit 1; }
    done
    compose run --rm --no-deps backend --check-schema
    compose run --rm --no-deps ai-api python -m scripts.check_schema
    if [[ "$(meta get classifier_enabled)" == true ]]; then
      compose run --rm --no-deps ai-worker python -c 'from app.classifier import load_classifier; load_classifier()'
    fi
    check_artifacts "$RELEASE_DIR"
    if [[ -n "$current" ]]; then
      # Drain before replacing any service. A busy solo worker can refuse control; abort safely.
      state draining
      drain_worker "$current"
    fi
    touch "$RELEASE_DIR/mutated"
    state starting
    compose up -d minio ai-redis
    compose run --rm --no-deps ai-api python -m scripts.check_storage
    compose run --rm -T --no-deps ai-api python -m scripts.production_ops check-ugtm-storage < "$RELEASE_DIR/backend-storage.json"
    compose up -d --wait --wait-timeout 180 --remove-orphans
    node=$(worker_node "$RELEASE_DIR")
    [[ -n "$node" ]]
    compose exec -T ai-worker celery -A app.celery_app.celery_app control add_consumer ugtm-ai-production --destination "$node" --timeout=10
    compose exec -T ai-worker celery -A app.celery_app.celery_app inspect ping --destination "$node" --timeout=10
    local_health "$RELEASE_DIR"
    state awaiting_external_checks
    ;;
  finalize)
    [[ -f "$RELEASE_DIR/mutated" && "$(cat "$APP_DIR/pending")" == "$RELEASE_DIR" ]]
    local_health "$RELEASE_DIR"
    old=$(cat "$RELEASE_DIR/previous-path")
    if validate_path "$old"; then ln -sfn "$old" "$APP_DIR/previous"; fi
    ln -sfn "$RELEASE_DIR" "$APP_DIR/current.next"
    mv -Tf "$APP_DIR/current.next" "$APP_DIR/current"
    rm -f "$APP_DIR/pending" "$RELEASE_DIR/mutated"
    state deployed
    ;;
  rollback)
    # No mutation (or an already recovered attempt) is an idempotent no-op.
    [[ -f "$RELEASE_DIR/mutated" ]] || exit 0
    rollback || { state recovery_required; exit 1; }
    ;;
  check-rollback|check-candidate-rollback|rollback-current)
    [[ "$MODE" == check-candidate-rollback || "$current" == "$RELEASE_DIR" ]]
    if [[ "$MODE" == check-candidate-rollback ]]; then
      [[ -f "$RELEASE_DIR/mutated" || "$(meta get deployment_status)" == rolled_back ]]
    fi
    old=$(cat "$RELEASE_DIR/previous-path")
    validate_path "$old"
    check_artifacts "$old"
    meta compatible "$old"
    compose_at "$old" run --rm --no-deps backend --check-schema
    compose_at "$old" run --rm --no-deps ai-api python -m scripts.check_schema
    if [[ "$MODE" == rollback-current ]]; then
      printf '%s\n' "$RELEASE_DIR" > "$APP_DIR/pending"
      touch "$RELEASE_DIR/mutated"
      rollback || { state recovery_required; exit 1; }
    fi
    ;;
  status) meta summary ;;
  *) echo 'Expected prepare, candidate, finalize, rollback, check-rollback, check-candidate-rollback, rollback-current or status.' >&2; exit 1 ;;
esac

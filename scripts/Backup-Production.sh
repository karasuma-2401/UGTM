#!/usr/bin/env bash
set -euo pipefail
umask 077
root="${UGTM_DEPLOY_ROOT:-$HOME/ugtm/production}"
exec 9>"$root/deploy.lock"
flock -n 9 || { echo 'Deployment/another operation is running.' >&2; exit 1; }
[[ ! -e "$root/pending" ]] || { echo 'Resolve the pending deployment first.' >&2; exit 1; }
python3 "$(dirname -- "${BASH_SOURCE[0]}")/production_backup.py" backup --root "$root"

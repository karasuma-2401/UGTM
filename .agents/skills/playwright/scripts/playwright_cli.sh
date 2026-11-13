#!/usr/bin/env bash
set -euo pipefail

if ! command -v npx >/dev/null 2>&1; then
  echo "Error: npx is required; install the project-supported Node.js 24.x/npm runtime." >&2
  exit 1
fi

has_session_flag="false"
for arg in "$@"; do
  case "$arg" in
    --session|--session=*|-s|-s=*)
      has_session_flag="true"
      break
      ;;
  esac
done

cmd=(npx --yes --package @playwright/cli@0.1.24 playwright-cli)
if [[ "${has_session_flag}" != "true" ]]; then
  cmd+=("-s=${PLAYWRIGHT_CLI_SESSION:-ugtm}")
fi
cmd+=("$@")

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../../.." && pwd)"
mkdir -p "$repository_root/artifacts/playwright"
cd "$repository_root/artifacts/playwright"
exec "${cmd[@]}"

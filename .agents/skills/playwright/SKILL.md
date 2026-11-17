---
name: "playwright"
description: "Use when the task requires automating a real browser from the terminal (navigation, form filling, snapshots, screenshots, data extraction, UI-flow debugging) via `playwright-cli` or the bundled wrapper script."
---


# Playwright CLI Skill

Drive a real browser from the terminal using `playwright-cli`. Prefer the bundled wrapper script so the CLI works even when it is not globally installed.
Treat this skill as CLI-first automation. Do not pivot to `@playwright/test` unless the user explicitly asks for test files.

## UGTM / PowerShell

On Windows use the sibling PowerShell wrapper instead of Bash. Node.js 24.x and npm/npx are required; a global CLI install is unnecessary.

```powershell
$pwcli = (Resolve-Path ./.agents/skills/playwright/scripts/playwright_cli.ps1).Path
& $pwcli '-s=ugtm-review' open http://localhost:5173 --browser msedge
& $pwcli '-s=ugtm-review' snapshot
& $pwcli '-s=ugtm-review' screenshot
& $pwcli '-s=ugtm-review' close
```

Both wrappers pin Playwright CLI to 0.1.24, run under the repository's ignored artifacts/playwright directory, and preserve CLI exit codes. Pass absolute paths for upload/config/profile/file arguments. Set PLAYWRIGHT_CLI_SESSION or use -s= to isolate tasks. Use existing browsers; do not silently install browsers or alter sandbox permissions. See [project setup](../../../docs/agent-skills.md).

## Runtime and wrapper paths

Node.js 24.x and npm/npx must be available. On PowerShell check Get-Command npx.cmd; on Bash check command -v npx. If the runtime is missing or inaccessible in the sandbox, report that failure and use the environment's approval mechanism where appropriate. Do not install a global CLI as a prerequisite: the project wrappers use the pinned CLI through npm cache.

On Bash, resolve the wrapper from the repository root before changing directories:

```bash
export PWCLI="$PWD/.agents/skills/playwright/scripts/playwright_cli.sh"
bash "$PWCLI" --help
bash "$PWCLI" open http://localhost:5173 --browser chrome
bash "$PWCLI" snapshot
bash "$PWCLI" close
```

On PowerShell use the Windows entrypoint above. Both entrypoints preserve the caller's working directory after the command, while browser output is written under artifacts/playwright/.

## Core workflow

1. Open the page.
2. Snapshot to get stable element refs.
3. Interact using refs from the latest snapshot.
4. Re-snapshot after navigation or significant DOM changes.
5. Capture artifacts (screenshot, pdf, traces) when useful.

Minimal loop:

```bash
bash "$PWCLI" open https://example.com
bash "$PWCLI" snapshot
bash "$PWCLI" click e3
bash "$PWCLI" snapshot
```

## When to snapshot again

Snapshot again after:

- navigation
- clicking elements that change the UI substantially
- opening/closing modals or menus
- tab switches

Refs can go stale. When a command fails due to a missing ref, snapshot again.

## Recommended patterns

### Form fill and submit

```bash
bash "$PWCLI" open https://example.com/form
bash "$PWCLI" snapshot
bash "$PWCLI" fill e1 "user@example.com"
bash "$PWCLI" fill e2 "password123"
bash "$PWCLI" click e3
bash "$PWCLI" snapshot
```

### Debug a UI flow with traces

```bash
bash "$PWCLI" open https://example.com --headed
bash "$PWCLI" tracing-start
# ...interactions...
bash "$PWCLI" tracing-stop
```

### Multi-tab work

```bash
bash "$PWCLI" tab-new https://example.com
bash "$PWCLI" tab-list
bash "$PWCLI" tab-select 0
bash "$PWCLI" snapshot
```

## Wrapper script

The wrapper script uses `npx --package @playwright/cli@0.1.24 playwright-cli` so the CLI can run without a global install:

```bash
bash "$PWCLI" --help
```

Prefer the wrapper unless the repository already standardizes on a global install.

## References

Open only what you need:

- CLI command reference: `references/cli.md`
- Practical workflows and troubleshooting: `references/workflows.md`

## Guardrails

- Always snapshot before referencing element ids like `e12`.
- Re-snapshot when refs seem stale.
- Prefer explicit commands over `eval` and `run-code` unless needed.
- When you do not have a fresh snapshot, use placeholder refs like `eX` and say why; do not bypass refs with `run-code`.
- Use `--headed` when a visual check will help.
- When capturing artifacts in this repo, use `artifacts/playwright/` and avoid introducing new top-level artifact folders.
- Default to CLI commands and workflows, not Playwright test specs.

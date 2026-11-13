# Pass CLI flags through without PowerShell parameter binding.
$ErrorActionPreference = 'Stop'
$cliArguments = @($args)
$runnerName = if ($env:OS -eq 'Windows_NT') { 'npx.cmd' } else { 'npx' }
$runner = Get-Command $runnerName -CommandType Application -ErrorAction SilentlyContinue
if (-not $runner) { throw 'npx is required. Install the project-supported Node.js 24.x/npm runtime first.' }

$hasSession = @($cliArguments | Where-Object { $_ -match '^(--session($|=)|-s($|=))' }).Count -gt 0
$forwarded = @()
if (-not $hasSession) {
    $sessionName = if ($env:PLAYWRIGHT_CLI_SESSION) { $env:PLAYWRIGHT_CLI_SESSION } else { 'ugtm' }
    $forwarded += "-s=$sessionName"
}
$forwarded += $cliArguments

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$artifactRoot = Join-Path $repositoryRoot 'artifacts/playwright'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
Push-Location -LiteralPath $artifactRoot
try {
    & $runner.Source --yes --package '@playwright/cli@0.1.24' playwright-cli @forwarded
    $result = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $result

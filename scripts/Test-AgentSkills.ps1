[CmdletBinding()]
param(
    [switch]$SmokeTest,
    [ValidateSet('chrome', 'msedge', 'firefox', 'webkit')]
    [string]$Browser = $(if ($env:OS -eq 'Windows_NT') { 'msedge' } else { 'chrome' })
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$skillRoot = Join-Path $repositoryRoot '.agents/skills'
$pythonRunner = Join-Path $PSScriptRoot 'Invoke-SkillPython.ps1'
$browserRunner = Join-Path $skillRoot 'playwright/scripts/playwright_cli.ps1'
$failures = [Collections.Generic.List[string]]::new()

function Invoke-Check {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
        Write-Host "PASS $Name"
    } catch {
        $failures.Add($Name)
        Write-Host "FAIL ${Name}: $($_.Exception.Message)"
    }
}

Invoke-Check 'Project skill files and metadata' {
    foreach ($name in @('impeccable', 'ui-ux-pro-max', 'playwright')) {
        $path = Join-Path $skillRoot "$name/SKILL.md"
        $content = Get-Content -LiteralPath $path -Raw
        if ($content -notmatch '(?s)\A---\r?\n(.+?)\r?\n---') { throw "Invalid frontmatter: $path" }
        $frontmatter = $Matches[1]
        if ($frontmatter -notmatch ('(?m)^name:\s*["'']?' + [regex]::Escape($name) + '["'']?\s*$')) { throw "Unexpected skill name: $path" }
        if ($frontmatter -notmatch '(?m)^description:\s*\S') { throw "Missing description: $path" }
        if ($frontmatter -match '(?m)^version:') { throw "Move top-level version into metadata: $path" }
    }
    foreach ($relative in @('playwright/scripts/playwright_cli.ps1', 'ui-ux-pro-max/scripts/search.py', 'ui-ux-pro-max/data/stacks/react.csv', 'impeccable/scripts/VERSION', 'impeccable/scripts/engine-checksums.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $skillRoot $relative) -PathType Leaf)) { throw "Missing resource: $relative" }
    }
}
Invoke-Check 'Project context' {
    foreach ($name in @('AGENTS.md', 'PRODUCT.md', 'DESIGN.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $name) -PathType Leaf)) { throw "Missing $name" }
    }
}
Invoke-Check 'Node.js 24 and npm runner' {
    $version = & node --version
    if ($LASTEXITCODE -ne 0 -or $version -notmatch '^v24\.') { throw "Expected Node.js 24.x, got $version" }
    $runner = if ($env:OS -eq 'Windows_NT') { 'npx.cmd' } else { 'npx' }
    Get-Command $runner -CommandType Application -ErrorAction Stop | Out-Null
    Write-Host "  Node $version"
}
Invoke-Check 'Python 3 runtime' {
    & $pythonRunner --version
    if ($LASTEXITCODE -ne 0) { throw 'Python returned an error.' }
}

if ($SmokeTest) {
    Invoke-Check 'UI/UX Pro Max local React search' {
        $search = Join-Path $skillRoot 'ui-ux-pro-max/scripts/search.py'
        $result = @(& $pythonRunner $search form --stack react -n 1)
        if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -notmatch 'Result 1') { throw 'React search did not return a result.' }
    }
    Invoke-Check 'Impeccable engine' {
        $launcher = if ($env:OS -eq 'Windows_NT') { 'impeccable.cmd' } else { 'impeccable' }
        $launcherPath = Join-Path $skillRoot "impeccable/scripts/$launcher"
        Push-Location -LiteralPath $repositoryRoot
        try {
            & $launcherPath engine-probe
            if ($LASTEXITCODE -ne 0) { throw 'Engine probe failed.' }
        } finally { Pop-Location }
    }
    Invoke-Check 'Playwright browser open, snapshot and close' {
        $session = '-s=ugtm-skills-check-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
        $opened = $false
        try {
            & $browserRunner $session open about:blank --browser $Browser --idle-timeout 60000
            if ($LASTEXITCODE -ne 0) { throw 'Browser could not open. Check browser installation, network/cache and execution permissions.' }
            $opened = $true
            & $browserRunner $session snapshot
            if ($LASTEXITCODE -ne 0) { throw 'Snapshot failed.' }
        } finally {
            if ($opened) {
                & $browserRunner $session close
                if ($LASTEXITCODE -ne 0) { throw 'Browser session could not close.' }
            }
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host ("Failed checks: " + ($failures -join ', '))
    exit 1
}
Write-Host 'Agent skill checks passed.'
exit 0

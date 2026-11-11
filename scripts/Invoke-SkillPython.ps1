# Keep arguments unbound so Python flags such as --stack and -c pass through unchanged.
$ErrorActionPreference = 'Stop'
$pythonArguments = @($args)
$candidates = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

if ($env:UGTM_PYTHON) {
    $candidates.Add(@{ Executable = $env:UGTM_PYTHON; Prefix = @() })
} else {
    foreach ($commandName in @('python', 'python3', 'py')) {
        foreach ($command in @(Get-Command $commandName -CommandType Application -All -ErrorAction SilentlyContinue)) {
            if ($command.Source -like '*WindowsApps*' -or -not $seen.Add($command.Source)) { continue }
            $prefix = @()
            if ($commandName -eq 'py') { $prefix = @('-3') }
            $candidates.Add(@{ Executable = $command.Source; Prefix = $prefix })
        }
    }
    # Per-user Python can be installed without an entry in the process PATH.
    if ($env:LOCALAPPDATA) {
        $pythonRoot = Join-Path $env:LOCALAPPDATA 'Programs/Python'
        try {
            $installations = @(Get-ChildItem -LiteralPath $pythonRoot -Directory -ErrorAction Stop | Sort-Object Name -Descending)
            foreach ($installation in $installations) {
                $executable = Join-Path $installation.FullName 'python.exe'
                if ((Test-Path -LiteralPath $executable) -and $seen.Add($executable)) {
                    $candidates.Add(@{ Executable = $executable; Prefix = @() })
                }
            }
        } catch {
            Write-Verbose "Per-user Python directory is unavailable: $($_.Exception.Message)"
        }
    }
}

foreach ($candidate in $candidates) {
    $executable = $candidate.Executable
    $prefix = @($candidate.Prefix)
    try {
        & $executable @prefix -B -X utf8 -c 'import sys; sys.exit(0 if sys.version_info.major == 3 else 1)' *> $null
        if ($LASTEXITCODE -ne 0) { continue }
    } catch {
        Write-Verbose "Python candidate could not run: $executable"
        continue
    }
    & $executable @prefix -B -X utf8 @pythonArguments
    exit $LASTEXITCODE
}

throw 'No runnable Python 3 was found. Check PATH or set UGTM_PYTHON to an existing Python executable. If Python exists outside the agent sandbox, request the required execution access; this helper does not install runtimes or change permissions.'

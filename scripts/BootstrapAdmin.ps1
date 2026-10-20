[CmdletBinding()]
param(
    [string]$Email,
    [string]$FullName = 'System Administrator',
    [string]$UserCode
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot '.env'
$previousBootstrapPassword = [Environment]::GetEnvironmentVariable('BOOTSTRAP_ADMIN_PASSWORD', 'Process')

if (Test-Path -LiteralPath $environmentFile) {
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*(DATABASE_CONNECTION_STRING|MIGRATION_CONNECTION_STRING|BOOTSTRAP_ADMIN_EMAIL|BOOTSTRAP_ADMIN_PASSWORD)\s*=(.*)$') {
            $variableName = $Matches[1]
            $variableValue = $Matches[2].Trim().Trim('"').Trim("'")
            if (-not [Environment]::GetEnvironmentVariable($variableName, 'Process') -and $variableValue) {
                [Environment]::SetEnvironmentVariable($variableName, $variableValue, 'Process')
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($Email)) {
    $Email = [Environment]::GetEnvironmentVariable('BOOTSTRAP_ADMIN_EMAIL', 'Process')
}
if ([string]::IsNullOrWhiteSpace($Email)) {
    $Email = Read-Host -Prompt 'Enter Admin Email'
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = [Environment]::GetEnvironmentVariable('BOOTSTRAP_ADMIN_PASSWORD', 'Process')
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    $securePassword = Read-Host -Prompt 'Enter Admin Password' -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ([string]::IsNullOrWhiteSpace($Email) -or [string]::IsNullOrWhiteSpace($Password)) {
    throw 'Admin Email and Password are required.'
}

Push-Location -LiteralPath $repositoryRoot
try {
    # Pass the secret to the child process through its environment, never as a
    # command-line argument where it could be exposed by process inspection.
    [Environment]::SetEnvironmentVariable('BOOTSTRAP_ADMIN_PASSWORD', $Password, 'Process')

    $cmdArgs = @('run', '--project', 'backend/host/Api/UGTM.Api.csproj', '--', 'bootstrap-admin',
        '--email', $Email,
        '--fullname', $FullName)

    if (-not [string]::IsNullOrWhiteSpace($UserCode)) {
        $cmdArgs += @('--usercode', $UserCode)
    }

    & dotnet @cmdArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Admin bootstrap failed with exit code $LASTEXITCODE."
    }
}
finally {
    [Environment]::SetEnvironmentVariable('BOOTSTRAP_ADMIN_PASSWORD', $previousBootstrapPassword, 'Process')
    $Password = $null
    Pop-Location
}

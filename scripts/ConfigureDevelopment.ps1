[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot '.env'
$connectionString = $env:ConnectionStrings__DefaultConnection
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    $connectionString = $env:DATABASE_CONNECTION_STRING
}
$privateKeyPath = $env:Identity__Jwt__PrivateKeyPath
if (Test-Path -LiteralPath $environmentFile) {
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*(DATABASE_CONNECTION_STRING|JWT_PRIVATE_KEY_FILE)\s*=(.*)$') {
            $variableName = $Matches[1]
            $variableValue = $Matches[2].Trim().Trim('"').Trim("'")
            if ($variableName -eq 'DATABASE_CONNECTION_STRING' -and [string]::IsNullOrWhiteSpace($connectionString)) {
                $connectionString = $variableValue
            }
            if ($variableName -eq 'JWT_PRIVATE_KEY_FILE' -and [string]::IsNullOrWhiteSpace($privateKeyPath)) {
                $privateKeyPath = $variableValue
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw 'Set DATABASE_CONNECTION_STRING in .env or the process environment before configuring development.'
}
if ([string]::IsNullOrWhiteSpace($privateKeyPath)) {
    $privateKeyPath = 'secrets/ugtm-jwt-private.pem'
}
if (-not [IO.Path]::IsPathRooted($privateKeyPath)) {
    $privateKeyPath = Join-Path $repositoryRoot $privateKeyPath
}
$privateKeyPath = [IO.Path]::GetFullPath($privateKeyPath)
if (-not (Test-Path -LiteralPath $privateKeyPath -PathType Leaf)) {
    throw 'JWT key not found. Run scripts/GenerateDevelopmentJwtKey.ps1 or configure an existing key path.'
}

$localConfiguration = @{
    ConnectionStrings = @{ DefaultConnection = $connectionString }
    Identity = @{ Jwt = @{ PrivateKeyPath = $privateKeyPath } }
} | ConvertTo-Json -Depth 4
$configurationPath = Join-Path $repositoryRoot 'backend/host/Api/appsettings.Development.local.json'
[IO.File]::WriteAllText($configurationPath, $localConfiguration, [Text.UTF8Encoding]::new($false))
Write-Host 'Configured Development database and JWT key for Rider and dotnet run.'

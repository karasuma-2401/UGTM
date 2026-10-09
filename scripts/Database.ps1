[CmdletBinding()]
param(
    [ValidateSet('List', 'Check', 'Add', 'Script', 'Update')]
    [string]$Action = 'List',
    [string]$MigrationName,
    [string]$SqlOutput = 'artifacts/migrations.sql'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentFile = Join-Path $repositoryRoot '.env'

if (Test-Path -LiteralPath $environmentFile) {
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*(DATABASE_CONNECTION_STRING|MIGRATION_CONNECTION_STRING)\s*=(.*)$') {
            $variableName = $Matches[1]
            $variableValue = $Matches[2].Trim().Trim('"').Trim("'")
            if (-not [Environment]::GetEnvironmentVariable($variableName, 'Process') -and $variableValue) {
                [Environment]::SetEnvironmentVariable($variableName, $variableValue, 'Process')
            }
        }
    }
}

Push-Location -LiteralPath $repositoryRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore the local EF Core tool.' }

    $efArguments = @('ef')
    switch ($Action) {
        'List' { $efArguments += @('migrations', 'list') }
        'Check' { $efArguments += @('migrations', 'has-pending-model-changes') }
        'Add' {
            if ($MigrationName -notmatch '^[A-Za-z][A-Za-z0-9_]*$') {
                throw 'Specify -MigrationName using letters, digits and underscores.'
            }
            $efArguments += @('migrations', 'add', $MigrationName)
        }
        'Script' {
            $sqlPath = if ([IO.Path]::IsPathRooted($SqlOutput)) { $SqlOutput } else { Join-Path $repositoryRoot $SqlOutput }
            $sqlDirectory = Split-Path -Parent $sqlPath
            New-Item -ItemType Directory -Force -Path $sqlDirectory | Out-Null
            $efArguments += @('migrations', 'script', '--idempotent', '--output', $sqlPath)
        }
        'Update' { $efArguments += @('database', 'update') }
    }

    $efArguments += @('--project', 'backend/Database.Migrations/UGTM.Database.Migrations.csproj',
        '--startup-project', 'backend/host/Api/UGTM.Api.csproj', '--context', 'UGTMDbContext')

    & dotnet @efArguments
    if ($LASTEXITCODE -ne 0) { throw "EF Core action '$Action' failed." }
}
finally {
    Pop-Location
}

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
$smtpHost = $env:Notifications__Smtp__Host
if ([string]::IsNullOrWhiteSpace($smtpHost)) { $smtpHost = $env:SMTP_HOST }
$smtpPort = $env:Notifications__Smtp__Port
if ([string]::IsNullOrWhiteSpace($smtpPort)) { $smtpPort = $env:SMTP_PORT }
$smtpFromAddress = $env:Notifications__Smtp__FromAddress
if ([string]::IsNullOrWhiteSpace($smtpFromAddress)) { $smtpFromAddress = $env:SMTP_FROM_ADDRESS }
$smtpFromName = $env:Notifications__Smtp__FromName
if ([string]::IsNullOrWhiteSpace($smtpFromName)) { $smtpFromName = $env:SMTP_FROM_NAME }
$smtpUsername = $env:Notifications__Smtp__Username
if ([string]::IsNullOrWhiteSpace($smtpUsername)) { $smtpUsername = $env:SMTP_USERNAME }
$smtpPassword = $env:Notifications__Smtp__Password
if ([string]::IsNullOrWhiteSpace($smtpPassword)) { $smtpPassword = $env:SMTP_PASSWORD }
$smtpSecurity = $env:Notifications__Smtp__Security
if ([string]::IsNullOrWhiteSpace($smtpSecurity)) { $smtpSecurity = $env:SMTP_SECURITY }
$smtpTimeoutSeconds = $env:Notifications__Smtp__TimeoutSeconds
if ([string]::IsNullOrWhiteSpace($smtpTimeoutSeconds)) { $smtpTimeoutSeconds = $env:SMTP_TIMEOUT_SECONDS }

if (Test-Path -LiteralPath $environmentFile) {
    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        if ($line -match '^\s*(DATABASE_CONNECTION_STRING|JWT_PRIVATE_KEY_FILE|SMTP_HOST|SMTP_PORT|SMTP_FROM_ADDRESS|SMTP_FROM_NAME|SMTP_USERNAME|SMTP_PASSWORD|SMTP_SECURITY|SMTP_TIMEOUT_SECONDS)\s*=(.*)$') {
            $variableName = $Matches[1]
            $variableValue = $Matches[2].Trim().Trim('"').Trim("'")
            switch ($variableName) {
                'DATABASE_CONNECTION_STRING' { if ([string]::IsNullOrWhiteSpace($connectionString)) { $connectionString = $variableValue } }
                'JWT_PRIVATE_KEY_FILE' { if ([string]::IsNullOrWhiteSpace($privateKeyPath)) { $privateKeyPath = $variableValue } }
                'SMTP_HOST' { if ([string]::IsNullOrWhiteSpace($smtpHost)) { $smtpHost = $variableValue } }
                'SMTP_PORT' { if ([string]::IsNullOrWhiteSpace($smtpPort)) { $smtpPort = $variableValue } }
                'SMTP_FROM_ADDRESS' { if ([string]::IsNullOrWhiteSpace($smtpFromAddress)) { $smtpFromAddress = $variableValue } }
                'SMTP_FROM_NAME' { if ([string]::IsNullOrWhiteSpace($smtpFromName)) { $smtpFromName = $variableValue } }
                'SMTP_USERNAME' { if ([string]::IsNullOrWhiteSpace($smtpUsername)) { $smtpUsername = $variableValue } }
                'SMTP_PASSWORD' { if ([string]::IsNullOrWhiteSpace($smtpPassword)) { $smtpPassword = $variableValue } }
                'SMTP_SECURITY' { if ([string]::IsNullOrWhiteSpace($smtpSecurity)) { $smtpSecurity = $variableValue } }
                'SMTP_TIMEOUT_SECONDS' { if ([string]::IsNullOrWhiteSpace($smtpTimeoutSeconds)) { $smtpTimeoutSeconds = $variableValue } }
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
}

if (-not [string]::IsNullOrWhiteSpace($smtpHost)) {
    if ([string]::IsNullOrWhiteSpace($smtpFromAddress)) {
        throw 'SMTP_FROM_ADDRESS is required when SMTP_HOST is configured.'
    }
    if ([string]::IsNullOrWhiteSpace($smtpFromName)) { $smtpFromName = 'UGTM' }
    if ([string]::IsNullOrWhiteSpace($smtpPort)) { $smtpPort = '587' }
    if ([string]::IsNullOrWhiteSpace($smtpSecurity)) { $smtpSecurity = 'StartTls' }
    if ([string]::IsNullOrWhiteSpace($smtpTimeoutSeconds)) { $smtpTimeoutSeconds = '30' }
    if ([string]::IsNullOrWhiteSpace($smtpUsername) -ne [string]::IsNullOrWhiteSpace($smtpPassword)) {
        throw 'SMTP_USERNAME and SMTP_PASSWORD must be configured together.'
    }

    $parsedSmtpPort = 0
    if (-not [int]::TryParse($smtpPort, [ref]$parsedSmtpPort)) {
        throw 'SMTP_PORT must be an integer.'
    }
    $parsedSmtpTimeout = 0
    if (-not [int]::TryParse($smtpTimeoutSeconds, [ref]$parsedSmtpTimeout)) {
        throw 'SMTP_TIMEOUT_SECONDS must be an integer.'
    }

    $localConfiguration.Notifications = @{
        Smtp = @{
            Host = $smtpHost
            Port = $parsedSmtpPort
            FromAddress = $smtpFromAddress
            FromName = $smtpFromName
            Username = $smtpUsername
            Password = $smtpPassword
            Security = $smtpSecurity
            TimeoutSeconds = $parsedSmtpTimeout
        }
    }
}

$localConfiguration = $localConfiguration | ConvertTo-Json -Depth 4
$configurationPath = Join-Path $repositoryRoot 'backend/host/Api/appsettings.Development.local.json'
[IO.File]::WriteAllText($configurationPath, $localConfiguration, [Text.UTF8Encoding]::new($false))
Write-Host 'Configured Development database, JWT key, and optional SMTP settings for Rider and dotnet run.'

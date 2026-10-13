[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\secrets\ugtm-jwt-private.pem'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$resolvedOutputPath = [IO.Path]::GetFullPath($OutputPath)

if ((Test-Path -LiteralPath $resolvedOutputPath) -and -not $Force) {
    Write-Host "JWT development key already exists: $resolvedOutputPath"
    exit 0
}

$outputDirectory = Split-Path -Parent $resolvedOutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.KeySize = 3072
    $pem = $rsa.ExportRSAPrivateKeyPem()
    [IO.File]::WriteAllText(
        $resolvedOutputPath,
        $pem,
        [Text.UTF8Encoding]::new($false))
}
finally {
    $rsa.Dispose()
}

Write-Host "Created JWT development key: $resolvedOutputPath"

# Run with PowerShell 7 from any directory. Never replaces an existing key pair.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$keyDirectory = Join-Path $PSScriptRoot '../.secrets/jwt'
$privatePath = Join-Path $keyDirectory 'private.pem'
$publicPath = Join-Path $keyDirectory 'public.pem'
if ((Test-Path -LiteralPath $privatePath) -or (Test-Path -LiteralPath $publicPath)) {
    if (!(Test-Path -LiteralPath $privatePath) -or !(Test-Path -LiteralPath $publicPath)) {
        throw 'Incomplete key pair. Restore the matching public/private files; keys were not overwritten.'
    }
    Write-Host 'Existing JWT key pair retained.'
    return
}
New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
$signingRsa = [Security.Cryptography.RSA]::Create(3072)
try {
    [IO.File]::WriteAllText($privatePath, $signingRsa.ExportPkcs8PrivateKeyPem())
    [IO.File]::WriteAllText($publicPath, $signingRsa.ExportSubjectPublicKeyInfoPem())
} finally { $signingRsa.Dispose() }
Write-Host 'Created JWT keys in ignored .secrets/jwt. Mount the private key only in Identity.'

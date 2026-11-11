[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $projectRoot '.env'
& (Join-Path $PSScriptRoot 'Initialize-JwtKeys.ps1')
if (Test-Path -LiteralPath $envFile) {
    Write-Host 'Existing .env retained. Compare new options with .env.example.'
    return
}
$template = [IO.File]::ReadAllLines((Join-Path $projectRoot '.env.example'))
$configured = foreach ($line in $template) {
    if ($line -match '^([A-Z][A-Z0-9_]+)=$') {
        $setting = $Matches[1]
        if ($setting -match '^(MSSQL_SA_PASSWORD|(?:IDENTITY|EQUIPMENT|MAINTENANCE|INCIDENT|NOTIFICATION)_DB_PASSWORD)$') {
            $setting + '=Aa9!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        } elseif ($setting -match '_KEY$') {
            $setting + '=' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        } else { $line }
    } else { $line }
}
[IO.File]::WriteAllLines($envFile, $configured)
Write-Host 'Created ignored .env with random database passwords and separate service keys. Values were not printed.'
Write-Host 'SMTP stays disabled. This script does not start services, apply migrations or create an Admin.'

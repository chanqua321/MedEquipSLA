[CmdletBinding()]
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Username,
    [ValidateSet('Identity','Equipment','Maintenance','Incident','Notification','All')]
    [string]$Service = 'All'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$services = if ($Service -eq 'All') { @('Identity','Equipment','Maintenance','Incident','Notification') } else { @($Service) }
foreach ($name in $services) {
    $sqlArgs = @('-S',$Server,'-d',"MedEquip_$name",'-C','-I','-b','-r','1')
    if ($Username) {
        if (-not $env:SQLCMDPASSWORD) { throw 'Set SQLCMDPASSWORD securely in the environment; do not pass passwords on the command line.' }
        $sqlArgs += @('-U',$Username)
    } else { $sqlArgs += '-E' }
    foreach ($kind in @('Schema','Behavior')) {
        & sqlcmd @sqlArgs -i (Join-Path $root "tests/database/$name-$kind.sql")
        if ($LASTEXITCODE -ne 0) { throw "$name $kind checks failed." }
    }
}
Write-Host 'PASS: selected service database checks. All test fixtures rolled back.'

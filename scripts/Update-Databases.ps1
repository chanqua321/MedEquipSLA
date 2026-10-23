[CmdletBinding()]
param(
    [ValidateSet('Identity','Equipment','Maintenance','Incident','Notification','All')]
    [string]$Service = 'All',
    [switch]$LocalDb,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
$originalConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Database')
try {
    $services = if ($Service -eq 'All') { @('Identity','Equipment','Maintenance','Incident','Notification') } else { @($Service) }
    # Validate every connection before applying any migration. Never print credentials.
    $connections = @{}
    foreach ($name in $services) {
        $connection = if ($LocalDb) {
            "Server=(localdb)\MSSQLLocalDB;Database=MedEquip_$name;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
        } else {
            [Environment]::GetEnvironmentVariable("MEDEQUIP_$($name.ToUpperInvariant())_MIGRATION_CONNECTION")
        }
        if ([string]::IsNullOrWhiteSpace($connection)) { throw "Missing migration connection for $name. Set MEDEQUIP_$($name.ToUpperInvariant())_MIGRATION_CONNECTION or use -LocalDb on Windows." }
        $connections[$name] = $connection
    }
    foreach ($name in $services) {
        $env:ConnectionStrings__Database = $connections[$name]
        $efArgs = @('--project',"src/Services/$name/$name.Infrastructure",'--startup-project',"src/Services/$name/$name.Api",'--context',"${name}DbContext")
        if ($NoBuild) { $efArgs += '--no-build' }
        Write-Host "Applying service-owned migrations: MedEquip_$name"
        & dotnet tool run dotnet-ef database update @efArgs
        if ($LASTEXITCODE -ne 0) { throw "Migration failed for $name. Later services were not applied." }
    }
} finally {
    if ([string]::IsNullOrEmpty($originalConnection)) {
        Remove-Item Env:\ConnectionStrings__Database -ErrorAction SilentlyContinue
    } else { $env:ConnectionStrings__Database = $originalConnection }
    Pop-Location
}

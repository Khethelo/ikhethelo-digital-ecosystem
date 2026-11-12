param([string]$TestPostgres = $env:IKD_TEST_POSTGRES, [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($TestPostgres)) {
    throw 'Provide IKD_TEST_POSTGRES for an isolated PostgreSQL test server with CREATE DATABASE permission.'
}
$backendRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location -LiteralPath $backendRoot
try {
    $env:IKD_TEST_POSTGRES = $TestPostgres
    dotnet build --no-restore --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet ef migrations has-pending-model-changes --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'EF model differs from the migration snapshot.' }
    dotnet test --no-build --no-restore --configuration $Configuration --logger 'trx;LogFileName=verification.trx' --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
finally { Pop-Location }

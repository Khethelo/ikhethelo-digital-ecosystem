param([int]$Port = 4200)
$ErrorActionPreference = 'Stop'
$frontendRoot = $PSScriptRoot
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
if (-not $nodeCommand) {
    $toolsRoot = Join-Path $frontendRoot '../.frontend-tools'
    $versionFile = Join-Path $toolsRoot 'versions.json'
    if (-not (Test-Path -LiteralPath $versionFile)) { throw 'Install Node.js 24.15 or later in the 24.x LTS line, then reopen the terminal.' }
    $versions = Get-Content -LiteralPath $versionFile -Raw | ConvertFrom-Json
    $env:PATH = (Join-Path $toolsRoot $versions.nodeDirectory) + ';' + $env:PATH
}
Push-Location -LiteralPath $frontendRoot
try {
    if (-not (Test-Path -LiteralPath 'node_modules/@angular/cli/bin/ng.js')) {
        npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
    }
    npm.cmd start -- --port $Port
    if ($LASTEXITCODE -ne 0) { throw 'Frontend development server exited with an error.' }
}
finally { Pop-Location }

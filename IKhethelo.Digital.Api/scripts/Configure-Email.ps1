$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot '../src/IKhethelo.Digital.Api/IKhethelo.Digital.Api.csproj'
$secret = Read-Host 'Paste your new Resend API key (input is hidden)' -AsSecureString
$credential = [System.Net.NetworkCredential]::new('', $secret)
$apiKey = $credential.Password.Trim()
if (-not $apiKey.StartsWith('re_')) { throw 'Expected a Resend API key starting with re_.' }
try {
    @{ 'Email:Password' = $apiKey } | ConvertTo-Json -Compress | dotnet user-secrets set --project $apiProject
    if ($LASTEXITCODE -ne 0) { throw 'Could not save the API key.' }
    Write-Host 'Saved the key in local User Secrets. Restart the backend to enable email.'
}
finally {
    $apiKey = $null
    $credential = $null
    $secret.Dispose()
}

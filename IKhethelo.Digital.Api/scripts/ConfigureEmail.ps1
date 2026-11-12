param(
    [string]$FromAddress,
    [string]$HostName = 'smtp.resend.com',
    [int]$Port = 587,
    [string]$Username = 'resend',
    [string]$FromName = "Es'hlahleni Social Club"
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($FromAddress)) {
    $FromAddress = Read-Host 'Verified sender email address'
}
try { $address = [System.Net.Mail.MailAddress]::new($FromAddress) }
catch { throw 'Enter a valid sender email address.' }
if ([string]::IsNullOrWhiteSpace($HostName) -or
    [string]::IsNullOrWhiteSpace($Username) -or $Port -lt 1 -or $Port -gt 65535) {
    throw 'Provide an SMTP host, username and port between 1 and 65535.'
}

$password = Read-Host 'SMTP password (Resend API key when using Resend)' -AsSecureString
if ($password.Length -eq 0) { throw 'An SMTP password is required.' }
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
try {
    # Pipe JSON to avoid putting the password in command arguments or shell history.
    $settings = @{
        'Email:Enabled' = 'true'
        'Email:Host' = $HostName
        'Email:Port' = $Port.ToString([Globalization.CultureInfo]::InvariantCulture)
        'Email:Username' = $Username
        'Email:Password' = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        'Email:FromAddress' = $address.Address
        'Email:FromName' = $FromName
    }
    $settings | ConvertTo-Json -Compress | & dotnet user-secrets set --id ikhethelo-digital-api-development
    if ($LASTEXITCODE -ne 0) { throw 'Could not save email settings.' }
}
finally {
    if ($null -ne $settings) { $settings.Clear() }
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $password.Dispose()
}
Write-Host 'Email settings saved in local user secrets. Restart the API in Development. New signups will attempt confirmation delivery.'

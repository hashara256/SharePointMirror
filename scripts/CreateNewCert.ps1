param (
    [string]$BaseSubject = "SharePointMirror PnPClient cert",
    [string]$CertOutputDir = "C:\Temp",
    [int]$YearsValid = 10
)

# --- Elevation Check ---
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Script must be run as Administrator."
    exit 1
}

# --- Prepare Unique Subject ---
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$subject = "CN=$BaseSubject-$timestamp"
$cerPath = Join-Path $CertOutputDir "$BaseSubject-$timestamp.cer"

Write-Host "Creating certificate with subject: $subject" -ForegroundColor Cyan

try {
    $cert = New-SelfSignedCertificate `
        -Subject $subject `
        -KeyExportPolicy Exportable `
        -KeySpec Signature `
        -KeyLength 2048 `
        -CertStoreLocation "Cert:\LocalMachine\My" `
        -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" `
        -NotAfter (Get-Date).AddYears($YearsValid) `
        -FriendlyName "$BaseSubject for SharePointMirror" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2")  # EKU: Client Authentication

    if (-not $cert) {
        throw "Certificate creation returned null."
    }

    $thumbprint = $cert.Thumbprint
    Write-Host "Certificate created. Thumbprint: $thumbprint" -ForegroundColor Yellow
}
catch {
    Write-Error "Certificate creation failed: $_"
    exit 2
}

try {
    Write-Host "Verifying certificate in store..." -ForegroundColor Cyan

    $verifiedCert = Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object { $_.Thumbprint -eq $thumbprint }

    if (-not $verifiedCert) {
        throw "Certificate not found in store after creation."
    }

    Write-Host "Certificate verified in store." -ForegroundColor Green
    $verifiedCert | Select-Object Subject, Thumbprint, NotAfter | Format-Table -AutoSize
}
catch {
    Write-Error "Store verification failed: $_"
    exit 3
}

try {
    Write-Host "Exporting public key (.cer) to: $cerPath" -ForegroundColor Cyan

    Export-Certificate `
        -Cert "Cert:\LocalMachine\My\$thumbprint" `
        -FilePath $cerPath -Force

    Write-Host "Export successful: $cerPath" -ForegroundColor Green
}
catch {
    Write-Error "Export to .cer failed: $_"
    exit 4
}

exit 0

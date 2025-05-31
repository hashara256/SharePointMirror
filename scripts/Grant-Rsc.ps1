<#
.SYNOPSIS
  Grants Resource-Specific Consent (RSC) for an AAD app on a single SharePoint site.

.DESCRIPTION
  Connects to the SharePoint admin endpoint via device-code OAuth,
  grants the specified AAD app the chosen permission level on the target site,
  and verifies the grant.

.PARAMETER AppId
  The AAD application (client) ID of your sync app.

.PARAMETER DisplayName
  A human-readable name for the grant entry.

.PARAMETER SiteUrl
  The full URL of the SharePoint site, e.g.
    https://contoso.sharepoint.com/sites/YourSite

.PARAMETER Permissions
  One of: Read, Write, Manage. Default: Write.

.PARAMETER Tenant
  Your tenant’s Azure AD domain, e.g.
    contoso.onmicrosoft.com

.EXAMPLE
  pwsh .\Grant-Rsc.ps1 `
    -AppId 12345678-90ab-cdef-1234-567890abcdef `
    -DisplayName "MySyncApp" `
    -SiteUrl "https://contoso.sharepoint.com/sites/YourSite" `
    -Permissions Write `
    -Tenant "contoso.onmicrosoft.com"
#>

param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$AppId,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$DisplayName,
    [Parameter(Mandatory)][ValidatePattern('^https:\/\/[^\/]+\/.+')][string]$SiteUrl,
    [ValidateSet("Read","Write","Manage")][string]$Permissions = "Write",
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Tenant
)

# derive admin endpoint
if ($SiteUrl -match '^https://([^/.]+)\.sharepoint\.com') {
    $adminUrl = "https://$($matches[1])-admin.sharepoint.com"
} else {
    Write-Error "Invalid SiteUrl; expected https://<tenant>.sharepoint.com/…"
    exit 1
}

# ensure PnP.PowerShell loaded
if (-not (Get-Module -ListAvailable -Name PnP.PowerShell)) {
    Write-Error "PnP.PowerShell module not found. Install via 'Install-Module PnP.PowerShell'"
    exit 1
}
Import-Module PnP.PowerShell -ErrorAction Stop

# connect via device code as Global Admin
Write-Host "Connecting to $adminUrl as Global Admin via device code…" -ForegroundColor Cyan
Connect-PnPOnline `
  -Url $adminUrl `
  -DeviceLogin `
  -Tenant $Tenant

# grant RSC
Write-Host "Granting '$Permissions' permission for app $AppId on $SiteUrl…" -ForegroundColor Cyan
$grant = Grant-PnPAzureADAppSitePermission `
  -AppId $AppId `
  -DisplayName $DisplayName `
  -Site $SiteUrl `
  -Permissions $Permissions

# verify
Write-Host "Verifying grant…" -ForegroundColor Cyan
$check = Get-PnPAzureADAppSitePermission -AppId $AppId -Site $SiteUrl

if ($check.Roles -contains $Permissions) {
    Write-Host "RSC grant successful: $Permissions on $SiteUrl" -ForegroundColor Green
    exit 0
}
else {
    Write-Error "RSC grant failed; current roles: $($check.Roles -join ', ')" 
    exit 1
}
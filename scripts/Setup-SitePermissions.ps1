<#
.SYNOPSIS
  Configures Azure AD and SharePoint permissions for a sync application.
.DESCRIPTION
  1. Grants "Sites.Selected" app role to the specified AAD app.
  2. Grants resource-specific consent (RSC) at the site level using PnP PowerShell.
.PARAMETER AppId
  The client ID of your Azure AD application.
.PARAMETER SiteUrl
  The full URL of the target SharePoint site. e.g. https://contoso.sharepoint.com/sites/YourSite
.PARAMETER Tenant
  Your Azure AD tenant domain. e.g. contoso.onmicrosoft.com
.PARAMETER Permissions
  RSC permission to grant: Read, Write, or Manage. Default: Write.
.PARAMETER DisplayName
  Optional friendly name for the RSC grant entry. Defaults to "SyncApp-<AppId>".
.EXAMPLE
  pwsh ./scripts/Setup-SharePointPermissions.ps1 \
    -AppId 12345678-90ab-cdef-1234-567890abcdef \
    -SiteUrl "https://contoso.sharepoint.com/sites/YourSite" \
    -Tenant contoso.onmicrosoft.com \
    -Permissions Write
#>
param(
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$AppId,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$SiteUrl,
    [Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$Tenant,
    [ValidateSet("Read","Write","Manage")][string]$Permissions = "Write",
    [string]$DisplayName = ""
)

if (-not $DisplayName) { $DisplayName = "SyncApp-$AppId" }

# Derive the admin endpoint
if ($SiteUrl -match '^https://([^/.]+)\.sharepoint\.com') {
    $tenantName = $matches[1]
    $adminUrl   = "https://$tenantName-admin.sharepoint.com"
} else {
    Write-Error "Invalid SiteUrl. Must be https://<tenant>.sharepoint.com/..."
    exit 1
}

# Ensure required modules
if (-not (Get-Module -ListAvailable -Name PnP.PowerShell)) {
    Write-Error "PnP.PowerShell not installed. Run: Install-Module PnP.PowerShell"
    exit 1
}
if (-not (Get-Module -ListAvailable -Name AzureAD)) {
    Write-Error "AzureAD module not installed. Run: Install-Module AzureAD"
    exit 1
}
Import-Module AzureAD -ErrorAction Stop
Import-Module PnP.PowerShell -ErrorAction Stop

# 1) Grant Sites.Selected at tenant level
Write-Host "Connecting to Azure AD ($Tenant) to assign Sites.Selected..." -ForegroundColor Cyan
Connect-AzureAD -TenantId $Tenant
$sp = Get-AzureADServicePrincipal -Filter "AppId eq '00000003-0000-0ff1-ce00-000000000000'"
$role = $sp.AppRoles | Where-Object { $_.Value -eq 'Sites.Selected' }
$appSp = Get-AzureADServicePrincipal -Filter "AppId eq '$AppId'"
$existing = Get-AzureADServiceAppRoleAssignment -ObjectId $appSp.ObjectId \
            | Where-Object { $_.ResourceId -eq $sp.ObjectId -and $_.Id -eq $role.Id }
if (-not $existing) {
    Write-Host "Assigning Sites.Selected to App $AppId..." -ForegroundColor Cyan
    New-AzureADServiceAppRoleAssignment -ObjectId $appSp.ObjectId -PrincipalId $appSp.ObjectId -ResourceId $sp.ObjectId -Id $role.Id
    Write-Host "✅ Sites.Selected assigned" -ForegroundColor Green
} else {
    Write-Host "Sites.Selected already assigned" -ForegroundColor Yellow
}

# 2) Grant RSC at site level
Write-Host "Connecting to SharePoint Admin ($adminUrl) for RSC grant..." -ForegroundColor Cyan
Connect-PnPOnline -Url $adminUrl -DeviceLogin -Tenant $Tenant

Write-Host "Granting '$Permissions' permission for app $AppId on site $SiteUrl..." -ForegroundColor Cyan
Grant-PnPAzureADAppSitePermission -AppId $AppId -DisplayName $DisplayName -Site $SiteUrl -Permissions $Permissions

Write-Host "Verifying RSC grant..." -ForegroundColor Cyan
$grant = Get-PnPAzureADAppSitePermission -AppId $AppId -Site $SiteUrl
if ($grant.Roles -contains $Permissions) {
    Write-Host "✅ RSC grant successful: $Permissions on $SiteUrl" -ForegroundColor Green
    exit 0
} else {
    Write-Error "❌ RSC grant failed; current roles: $($grant.Roles -join ', ')"
    exit 1
}

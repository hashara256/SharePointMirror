<#
.SYNOPSIS
  Verifies that an Azure AD app has resource-specific consent (RSC) on a SharePoint site.
.DESCRIPTION
  - Installs and imports PnP.PowerShell if needed.
  - Detects OS and prompts the user (Global Admin) to authenticate interactively or via device code.
  - Checks that the specified AAD app has an RSC grant on the target site and expected role.
.PARAMETER AppId
  The client ID of the Azure AD application to verify.
.PARAMETER SiteUrl
  The full URL of the SharePoint site (e.g. https://contoso.sharepoint.com/sites/YourSite).
.PARAMETER Tenant
  The Azure AD tenant domain (e.g. contoso.onmicrosoft.com).
.PARAMETER Permissions
  The expected RSC role: Read, Write, or Manage. Default: Write.
.EXAMPLE
  pwsh ./scripts/Verify-SharePointPermissions.ps1 `
    -AppId "12345678-90ab-cdef-1234-567890abcdef" `
    -SiteUrl "https://contoso.sharepoint.com/sites/YourSite" `
    -Tenant "contoso.onmicrosoft.com" `
    -Permissions Write
#>
param(
    [Parameter(Mandatory=$true)] [string] $AppId,
    [Parameter(Mandatory=$true)][ValidatePattern('^https:\/\/[^\/]+\/.+')] [string] $SiteUrl,
    [Parameter(Mandatory=$true)] [string] $Tenant,
    [ValidateSet('Read','Write','Manage')] [string] $Permissions = 'Write'
)

# Ensure PnP.PowerShell is installed and imported
if (-not (Get-Module -ListAvailable -Name PnP.PowerShell)) {
    Write-Host "Installing PnP.PowerShell module...";
    Install-Module -Name PnP.PowerShell -Scope CurrentUser -Force -ErrorAction Stop;
}
Import-Module PnP.PowerShell -ErrorAction Stop;

# Derive admin URL from SiteUrl
$regex = [regex] '^https://([^/.]+)\.sharepoint\.com';
$match = $regex.Match($SiteUrl);
if (-not $match.Success) {
    Write-Error "Invalid SiteUrl. Expected format https://<tenant>.sharepoint.com/...";
    exit 1;
}
$tenantName = $match.Groups[1].Value;
$adminUrl   = "https://$tenantName-admin.sharepoint.com";

# Authenticate as Global Admin
if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    Write-Host "Please sign in interactively as a Global Admin...";
    Connect-PnPOnline -Url $adminUrl -Interactive -Tenant $Tenant -ErrorAction Stop;
} else {
    Write-Host "Please complete device-code authentication as Global Admin...";
    Connect-PnPOnline -Url $adminUrl -DeviceLogin -ErrorAction Stop;
}

# Retrieve RSC grant
try {
    $grant = Get-PnPAzureADAppSitePermission -AppId $AppId -Site $SiteUrl;
} catch {
    Write-Error "Failed to retrieve RSC grant: $($_.Exception.Message)";
    exit 1;
}

if (-not $grant) {
    Write-Error "No RSC grant found for AppId $AppId on $SiteUrl.";
    Write-Error "Run Setup-SharePointPermissions.ps1 to assign needed permissions.";
    exit 1;
}

# Verify expected role
if ($grant.Roles -contains $Permissions) {
    Write-Host "✅ RSC role '$Permissions' is present on $SiteUrl.";
    exit 0;
} else {
    Write-Error "Expected RSC role '$Permissions' not found. Current roles: $($grant.Roles -join ', ')";
    Write-Error "Re-run Setup-SharePointPermissions.ps1 with '-Permissions $Permissions'.";
    exit 1;
}

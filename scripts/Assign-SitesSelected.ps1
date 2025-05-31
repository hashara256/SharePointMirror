<#
.SYNOPSIS
  Assigns the Microsoft Graph/SPO Sites.Selected application permission to an existing AAD app registration.

.DESCRIPTION
  Connects to Azure AD via device-code (or interactive) as a Global Admin,
  locates the built-in SharePoint service principal and its "Sites.Selected" appRole,
  then assigns that appRole to the target application.

.PARAMETER AppId
  The client ID of your existing AAD application to receive Sites.Selected.

.PARAMETER Tenant
  Your Azure AD tenant domain (e.g. contoso.onmicrosoft.com).

.EXAMPLE
  pwsh ./scripts/Assign-SitesSelected.ps1 -AppId 12345678-90ab-cdef-1234-567890abcdef -Tenant contoso.onmicrosoft.com
#>
param(
    [Parameter(Mandatory=$true)][string]$AppId,
    [Parameter(Mandatory=$true)][string]$Tenant
)

# Connect to Azure AD (device code flow)
Write-Host "Connecting to Azure AD as Global Admin..." -ForegroundColor Cyan
Connect-AzureAD -TenantId $Tenant

# Locate the SharePoint service principal
$sp = Get-AzureADServicePrincipal -Filter "AppId eq '00000003-0000-0ff1-ce00-000000000000'"
if (-not $sp) { throw "SharePoint service principal not found." }

# Find the Sites.Selected appRole
$role = $sp.AppRoles | Where-Object { $_.Value -eq 'Sites.Selected' } | Select-Object -First 1
if (-not $role) { throw "Sites.Selected appRole not found on SharePoint service principal." }

# Locate the target application’s service principal
$appSp = Get-AzureADServicePrincipal -Filter "AppId eq '$AppId'"
if (-not $appSp) { throw "Service principal for AppId $AppId not found."
}

# Assign the Sites.Selected appRole to the app’s service principal
Write-Host "Assigning Sites.Selected to app $AppId..." -ForegroundColor Cyan
New-AzureADServiceAppRoleAssignment `
    -ObjectId $appSp.ObjectId `
    -PrincipalId $appSp.ObjectId `
    -ResourceId $sp.ObjectId `
    -Id $role.Id
Write-Host "✅ Sites.Selected assigned." -ForegroundColor Green

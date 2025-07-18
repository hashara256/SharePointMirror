[![.NET](https://github.com/hashara256/SharePointMirror/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hashara256/SharePointMirror/actions/workflows/dotnet.yml)

# SharePointMirror

Headless .NET 8 Worker Service for mirroring and processing files from SharePoint Online document libraries.

SharePointMirror is a cross-platform background service that polls a SharePoint Online document library, downloads new files matching a configurable prefix (recursively), verifies their SHA-256 hash, and moves or deletes originals as configured. It preserves the folder structure on disk and is suitable for running as a Windows Service or Linux daemon.

---

## Features

- Authenticates with Microsoft Entra ID (certificate-based, app-only)
- Recursively processes SharePoint document libraries
- Configurable file prefix filter and folder ignore list
- SHA-256 hash verification (optional)
- Post-processing: move to _Done_/_Error_ or delete originals
- Runs on Windows or Linux, as a service or interactively
- Flexible logging (console, file, etc.)

---

## Releases

| Platform       | Deployment Type       | Download                     |
| -------------- | --------------------- | ---------------------------- |
| Windows x64    | Framework-Dependent   | [windows-net8.0.zip]         |
| Windows x64    | Self-Contained        | [windows-selfcontained.zip]  |
| Linux x64      | Framework-Dependent   | [linux-net8.0.tar.gz]        |
| Linux x64      | Self-Contained        | [linux-selfcontained.tar.gz] |

---

## Prerequisites

- Microsoft Entra ID
  - App Registration (single-tenant)
  - Application permission: SharePoint → Sites.FullControl.All
  - Admin consent granted
  - Certificate uploaded (public .cer)
- Certificate
  - PFX with private key (exported from your .cer/.key)
  - Accessible by the service account
- .NET 8 Runtime (for framework-dependent builds)
- Windows 10/Server 2016+ or modern Linux

---

## Entra ID App Registration

1. Register a new app in the Microsoft Entra ID portal.
2. Note the Application (client) ID and Directory (tenant) ID.
3. Upload your .cer file under Certificates & secrets.
4. Under API permissions, add Sites.FullControl.All (application), and grant admin consent.

---

## Certificate

Example: Generate a self-signed PFX (Linux)
# 1. Create key + cert
openssl req -x509 -nodes -days 365 \
  -newkey rsa:2048 \
  -keyout spm.key \
  -out spm.crt \
  -subj "/CN=SharePointMirror"

# 2. Bundle into PFX
openssl pkcs12 -export \
  -out SharePointMirrorAuth.pfx \
  -inkey spm.key \
  -in spm.crt \
  -passout pass:YourPfxPassword
* **Upload** `spm.crt` to Azure AD (public).
* **Copy** `SharePointMirrorAuth.pfx` and note its password.

---

## ⚙️ Configuration

Unzip your chosen asset. In the root folder, edit **appsettings.json**:
{
  "Logging": {
    "LogLevel": { "Default": "Information" }
  },
  "SharePoint": {
    "SiteUrl":     "https://contoso.sharepoint.com/sites/YourSite",
    "LibraryRoot": "/Shared Documents",
    "ClientId":    "YOUR-CLIENT-ID",
    "TenantId":    "YOUR-TENANT-ID",
    "AuthMode":    "Certificate",
    "PfxPath":     "C:/certs/SharePointMirrorAuth.pfx",
    "PfxPassword": "YourPfxPassword",
    "CertThumbprint": "",
    "CertStoreLocation": "",
    "CertStoreName":  ""
  },
  "Tracking": {
    "FilePrefix":          "SCAN_",
    "LocalRootPath":       "D:/SharePointMirror",
    "VerifyHash":          true,
    "DeleteIfMatch":       true,
    "IgnoreFolders":       [ "Forms", "_done" ],
    "PollIntervalSeconds": 300
  }
}
* **AuthMode**: `"Certificate"` (must match your config).
* **PfxPath** / **PfxPassword**: path & password to your `.pfx`.
* **LibraryRoot**: server-relative path; e.g. `"/Shared Documents"` or `"/sites/.../Documents"`.
* **FilePrefix**: only files beginning with this string are processed.
* **LocalRootPath**: where files are saved (folder structure preserved).
* **PollIntervalSeconds**: frequency between sync runs.

---

## Authentication Configuration

This application supports certificate-based authentication for SharePoint Online using either:

- **Windows Certificate Store** (recommended for production on Windows)
- **PFX File** (cross-platform, required on Linux)

### Configuration

#### 1. PFX File (Cross-Platform)

Set the following in your `appsettings.json`:

---

## ▶️ Running

### Interactive

#### Windows (Framework-Dependent)

1. Extract `windows-net8.0.zip`.
2. Open PowerShell, `cd` into folder.
3. `.\SharePointMirror.exe`

#### Linux (Framework-Dependent)
tar zxvf linux-net8.0.tar.gz
cd linux-net8.0
dotnet run
> Omit `dotnet` if using self-contained build; run the native exe directly.

---

## 🖥️ Windows Service

# From an elevated shell:
sc.exe create SharePointMirror \
  binPath= "\"C:\path\to\SharePointMirror.exe\" --console" \
  DisplayName= "SharePoint Mirror" start= auto

sc.exe start SharePointMirror
Or use **NSSM**:
nssm install SharePointMirror "C:\path\to\SharePointMirror.exe"
nssm set SharePointMirror Start SERVICE_AUTO_START
nssm start SharePointMirror

---

## 🐧 Linux Service & Logging

SharePointMirror can run as a background service (daemon) on Linux using **systemd**.

- **Service Setup:**  
  Create a systemd service file (e.g., `/etc/systemd/system/sharepointmirror.service`) that runs your app using `dotnet` or the self-contained binary.

- **Logging:**  
  By default, all logs are written to the console (stdout/stderr). When running under systemd, these logs are automatically captured and made available via the system journal.  
  View logs with:`sudo journalctl -u sharepointmirror -f`
No extra logging configuration is required for this default behavior.

- **Syslog/File Logging:**  
  If you want to log to syslog or a file in addition to the journal, add a logging provider (e.g., Serilog with a syslog or file sink).

---

## 📖 Logging & Maintenance

* By default logs to console. For file logging, add a provider in `appsettings.json` (e.g. Serilog).
* On Windows, logs are sent to the Windows Event Viewer when running as a service.
* On Linux, logs are available via `journalctl` when running as a systemd service.
* Use `LogLevel.Debug` during troubleshooting; switch back to `Information` in production to reduce disk use.

---

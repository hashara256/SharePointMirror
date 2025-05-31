[![.NET](https://github.com/hashara256/SharePointMirror/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hashara256/SharePointMirror/actions/workflows/dotnet.yml)

# SharePointMirror

**Headless .NET worker that mirrors a SharePoint document library to local storage**

SharePointMirror is a small, cross-platform .NET 8 console worker that polls a SharePoint Online library, downloads new files matching a configurable prefix (recursively through folders), optionally verifies their SHA-256 hash, deletes originals if desired, and preserves folder structure on disk. It runs on Linux or Windows—either interactively or as a service.

---

## 📥 Release Assets

| Platform       | Deployment Type       | Download                                                             |
| -------------- | --------------------- | -------------------------------------------------------------------- |
| Windows x64    | Framework-Dependent   | [windows-net8.0.zip](#)                                              |
| Windows x64    | Self-Contained (x64)  | [windows-selfcontained.zip](#)                                       |
| Linux x64      | Framework-Dependent   | [linux-net8.0.tar.gz](#)                                             |
| Linux x64      | Self-Contained (x64)  | [linux-selfcontained.tar.gz](#)                                      |

---

## ⚙️ Prerequisites

- **Entra**  
  - A **single-tenant** App Registration with:  
    - **Application** permission **SharePoint → Sites.FullControl.All**  
    - **Grant Admin Consent**  
  - Upload a **public cert (.cer)** under **Certificates & secrets**.  
- **Certificate**  
  - A matching **PFX** containing private key (Linux: `openssl pkcs12 …`, Windows: Certificate MMC → Export)  
- **.NET 8** runtime installed (if using framework-dependent build)  
- **Windows 10/Server 2016+** or any modern Linux distro  

---

## 🛠️ Entra ID App Registration

1. In the Entra ID Portal, go to **App registrations → New registration**.  
2. Note the **Application (client) ID** and **Directory (tenant) ID**.  
3. Under **Certificates & secrets → Certificates**, click **Upload certificate** and import your `.cer`.  
4. Under **API permissions → Add → SharePoint Online → Application permissions**, check **Sites.FullControl.All**, then **Grant admin consent**.  

---

## 🔐 Certificate

### Generate a self-signed PFX (Linux example)

```bash
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
````

* **Upload** `spm.crt` to Azure AD (public).
* **Copy** `SharePointMirrorAuth.pfx` and note its password.

---

## ⚙️ Configuration

Unzip your chosen asset. In the root folder, edit **appsettings.json**:

```json
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
```

* **AuthMode**: `"Certificate"` (must match your config).
* **PfxPath** / **PfxPassword**: path & password to your `.pfx`.
* **LibraryRoot**: server-relative path; e.g. `"/Shared Documents"` or `"/sites/.../Documents"`.
* **FilePrefix**: only files beginning with this string are processed.
* **LocalRootPath**: where files are saved (folder structure preserved).
* **PollIntervalSeconds**: frequency between sync runs.

---

## ▶️ Running

### Interactive

#### Windows (Framework-Dependent)

1. Extract `windows-net8.0.zip`.
2. Open PowerShell, `cd` into folder.
3. `.\SharePointMirror.exe`

#### Linux (Framework-Dependent)

```bash
tar zxvf linux-net8.0.tar.gz
cd linux-net8.0
dotnet run
```

> Omit `dotnet` if using self-contained build; run the native exe directly.

---

## 🖥️ Windows Service

```powershell
# From an elevated shell:
sc.exe create SharePointMirror \
  binPath= "\"C:\path\to\SharePointMirror.exe\" --console" \
  DisplayName= "SharePoint Mirror" start= auto

sc.exe start SharePointMirror
```

Or use **NSSM**:

```powershell
nssm install SharePointMirror "C:\path\to\SharePointMirror.exe"
nssm set SharePointMirror Start SERVICE_AUTO_START
nssm start SharePointMirror
```

---

## 📖 Logging & Maintenance

* By default logs to console. For file logging, add a provider in `appsettings.json` (e.g. Serilog).
* Use `LogLevel.Debug` during troubleshooting; switch back to `Information` in production to reduce disk use.

---

## 🐞 Troubleshooting

* **401 Unauthorized** → confirm `AuthMode` = `"Certificate"`, correct PFX, Azure AD certificate, and Sites.FullControl.All app permission.
* **ServerRelativeUrl error** → ensure `LibraryRoot` starts with `/` and site URL is correct.
* **No files processed** → verify `FilePrefix`, library path, and that files exist under that prefix.

---

## ❤️ Support & Contributing

* Open an issue on GitHub for bugs or feature requests.
* PRs welcome—follow existing code style.


```

// Options/SharePointOptions.cs
namespace SharePointMirror.Options
{
    /// <summary>
    /// Configuration options for connecting to SharePoint.
    /// </summary>
    public class SharePointOptions
    {
        /// <summary>The full URL of the SharePoint site (e.g. https://contoso.sharepoint.com/sites/YourSite).</summary>
        public string SiteUrl { get; set; } = string.Empty;

        /// <summary>The server-relative path to the document library root (e.g. '/Shared Documents').</summary>
        public string LibraryRoot { get; set; } = string.Empty;

        /// <summary>The Azure AD application (client) ID.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>The Azure AD tenant (directory) ID.</summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>Authentication mode: 'ClientSecret' or 'Certificate'.</summary>
        public string AuthMode { get; set; } = "ClientSecret";

        /// <summary>The client secret for Azure AD app-only authentication.</summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Path to the PFX file containing the certificate and private key.</summary>
        public string PfxPath { get; set; } = string.Empty;

        /// <summary>Password for the PFX file.</summary>
        public string PfxPassword { get; set; } = string.Empty;

        // Legacy properties left blank
        public string CertThumbprint { get; set; } = string.Empty;
        public string CertStoreLocation { get; set; } = string.Empty;
        public string CertStoreName { get; set; } = string.Empty;
    }
}

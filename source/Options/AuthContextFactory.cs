// Services/AuthContextFactory.cs
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using SharePointMirror.Options;

namespace SharePointMirror.Services
{
    /// <summary>
    /// Factory that creates a SharePoint ClientContext using certificate-based app-only authentication.
    /// </summary>
    public class AuthContextFactory : IAuthContextFactory
    {
        private readonly SharePointOptions _sp;
        private readonly ILogger<AuthContextFactory> _log;
        private readonly IConfidentialClientApplication _app;
        private readonly string[] _scopes;

        public AuthContextFactory(
            IOptions<SharePointOptions> sp,
            ILogger<AuthContextFactory> log)
        {
            _sp = sp.Value;
            _log = log;

            var authority = new Uri(new Uri(_sp.SiteUrl).GetLeftPart(UriPartial.Authority)).Host;
            _scopes = new[] { $"https://{authority}/.default" };

            X509Certificate2 cert = null;

            if (_sp.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer PFX if provided, otherwise use store (Windows only)
                if (!string.IsNullOrWhiteSpace(_sp.PfxPath))
                {
                    if (!System.IO.File.Exists(_sp.PfxPath))
                    {
                        _log.LogError("PFX file not found at {PfxPath}", _sp.PfxPath);
                        throw new InvalidOperationException($"PFX file not found: {_sp.PfxPath}");
                    }
                    cert = new X509Certificate2(_sp.PfxPath, _sp.PfxPassword, X509KeyStorageFlags.Exportable);
                    _log.LogInformation("Loaded certificate from PFX file: {Subject}, Thumbprint={Thumbprint}", cert.Subject, cert.Thumbprint);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var thumbprint = _sp.CertThumbprint?.Replace(" ", "").ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(thumbprint))
                    {
                        _log.LogError("Certificate thumbprint is not configured or empty.");
                        throw new InvalidOperationException("Certificate thumbprint must be specified.");
                    }

                    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);

                    cert = store.Certificates
                        .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
                        .OfType<X509Certificate2>()
                        .FirstOrDefault();

                    if (cert == null)
                    {
                        _log.LogError("Certificate with thumbprint {Thumbprint} not found in LocalMachine store.", thumbprint);
                        throw new InvalidOperationException($"Certificate with thumbprint {thumbprint} not found.");
                    }

                    if (!cert.HasPrivateKey)
                    {
                        _log.LogError("Certificate {Thumbprint} does not contain a private key.", thumbprint);
                        throw new InvalidOperationException($"Certificate {thumbprint} missing private key.");
                    }

                    _log.LogInformation("Certificate loaded from store: {Subject}, Thumbprint={Thumbprint}", cert.Subject, cert.Thumbprint);
                }
                else
                {
                    _log.LogError("No valid certificate source found. On Linux, PfxPath must be provided.");
                    throw new InvalidOperationException("On Linux, PfxPath must be provided for certificate authentication.");
                }
            }
            else if (_sp.AuthMode.Equals("ClientSecret", StringComparison.OrdinalIgnoreCase))
            {
                // ClientSecret not supported. I could not get it to work with SharePoint.
                _log.LogError("Unsupported AuthMode configured: {AuthMode}", _sp.AuthMode);
            }
            else
            {
                _log.LogError("Unsupported AuthMode configured: {AuthMode}", _sp.AuthMode);
                throw new InvalidOperationException($"Unsupported AuthMode '{_sp.AuthMode}'");
            }

            // Build the confidential client application ONCE
            _app = ConfidentialClientApplicationBuilder
                .Create(_sp.ClientId)
                .WithCertificate(cert)
                .WithTenantId(_sp.TenantId)
                .Build();
        }

        public ClientContext CreateContext()
        {
            _log.LogDebug("Initializing SharePoint authentication context (Mode: {AuthMode})", _sp.AuthMode);

            if (!_sp.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
            {
                _log.LogError("Unsupported AuthMode configured: {AuthMode}", _sp.AuthMode);
                throw new InvalidOperationException($"Unsupported AuthMode '{_sp.AuthMode}'");
            }

            AuthenticationResult result;
            try
            {
                // MSAL will reuse a valid token if available
                result = _app.AcquireTokenForClient(_scopes).ExecuteAsync().GetAwaiter().GetResult();

                var tokenSource = result.AuthenticationResultMetadata?.TokenSource.ToString() ?? "Unknown";
                _log.LogDebug("Access token {TokenSource}. Expires at: {ExpiresOn}", 
                    tokenSource == "Cache" ? "reused from cache" : "acquired from Entra ID", 
                    result.ExpiresOn);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Token acquisition failed using certificate authentication.");
                throw new InvalidOperationException("Token acquisition failed.", ex);
            }

            try
            {
                var ctx = new ClientContext(_sp.SiteUrl);
                ctx.ExecutingWebRequest += (sender, args) =>
                {
                    args.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + result.AccessToken;
                };

                _log.LogDebug("ClientContext successfully created for {SiteUrl}", _sp.SiteUrl);
                return ctx;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to initialize ClientContext for SharePoint site {SiteUrl}", _sp.SiteUrl);
                throw;
            }
        }
    }
}

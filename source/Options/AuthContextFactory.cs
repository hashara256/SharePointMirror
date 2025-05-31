// Services/AuthContextFactory.cs
using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using SharePointMirror.Options;

namespace SharePointMirror.Services
{
    /// <summary>
    /// Factory that creates a SharePoint ClientContext using either client-secret or certificate-based app-only authentication.
    /// </summary>
    public class AuthContextFactory : IAuthContextFactory
    {
        private readonly SharePointOptions _sp;
        private readonly ILogger<AuthContextFactory> _log;

        public AuthContextFactory(
            IOptions<SharePointOptions> sp,
            ILogger<AuthContextFactory> log)
        {
            _sp  = sp.Value;
            _log = log;
        }

        public ClientContext CreateContext()
        {
            _log.LogDebug("AuthMode={AuthMode}, ClientId={ClientId}", _sp.AuthMode, _sp.ClientId);

            // Determine authority and scope for MSAL
            var authority = new Uri(new Uri(_sp.SiteUrl).GetLeftPart(UriPartial.Authority)).Host;
            string[] scopes = { $"https://{authority}/.default" };

            AuthenticationResult result;

            if (_sp.AuthMode.Equals("Certificate", StringComparison.OrdinalIgnoreCase))
            {
                // Load PFX certificate with private key
                var cert = new X509Certificate2(_sp.PfxPath, _sp.PfxPassword, X509KeyStorageFlags.MachineKeySet);
                var app = ConfidentialClientApplicationBuilder
                    .Create(_sp.ClientId)
                    .WithCertificate(cert)
                    .WithTenantId(_sp.TenantId)
                    .Build();
                result = app.AcquireTokenForClient(scopes).ExecuteAsync().GetAwaiter().GetResult();
            }
            else if (_sp.AuthMode.Equals("ClientSecret", StringComparison.OrdinalIgnoreCase))
            {
                var app = ConfidentialClientApplicationBuilder
                    .Create(_sp.ClientId)
                    .WithClientSecret(_sp.ClientSecret)
                    .WithTenantId(_sp.TenantId)
                    .Build();
                result = app.AcquireTokenForClient(scopes).ExecuteAsync().GetAwaiter().GetResult();

                // Check appidacr claim
                try
                {
                    var parts = result.AccessToken.Split('.');
                    if (parts.Length > 1)
                    {
                        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("appidacr", out var acr) && acr.GetInt32() == 1)
                        {
                            throw new InvalidOperationException(
                                "Client-secret tokens (appidacr=1) are rejected by SharePoint CSOM. " +
                                "Use certificate-based authentication (appidacr=2) instead.");
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Unable to validate appidacr claim.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unsupported AuthMode '{_sp.AuthMode}'");
            }

            // // Log roles for verification
            // try
            // {
            //     var parts = result.AccessToken.Split('.');
            //     if (parts.Length > 1)
            //     {
            //         var json = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            //         using var doc = JsonDocument.Parse(json);
            //         if (doc.RootElement.TryGetProperty("roles", out var roles))
            //         {
            //             _log.LogInformation("Token roles: {Roles}", roles.ToString());
            //         }
            //     }
            // }
            // catch (Exception ex)
            // {
            //     _log.LogWarning(ex, "Failed parsing token roles.");
            // }

            // Create CSOM context and inject bearer token
            var ctx = new ClientContext(_sp.SiteUrl);
            ctx.ExecutingWebRequest += (sender, args) =>
            {
                args.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + result.AccessToken;
            };

            return ctx;
        }
    }
}

namespace SharePointMirror.Services
{
    using Microsoft.SharePoint.Client;

    /// <summary>
    /// Factory for creating an authenticated SharePoint ClientContext.
    /// </summary>
    public interface IAuthContextFactory
    {
        /// <summary>
        /// Creates and returns a SharePoint ClientContext.
        /// </summary>
        ClientContext CreateContext();
    }
}
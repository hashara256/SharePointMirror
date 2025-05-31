namespace SharePointMirror.Services
{
    using Microsoft.SharePoint.Client;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Processes a SharePoint folder: traversal and file handling.
    /// </summary>
    public interface IFolderProcessor
    {
        /// <summary>
        /// Recursively processes the given SharePoint folder context.
        /// </summary>
        /// <param name="ctx">Authenticated ClientContext.</param>
        /// <param name="token">Cancellation token.</param>
        Task ProcessFolderAsync(ClientContext ctx, CancellationToken token);
    }
}

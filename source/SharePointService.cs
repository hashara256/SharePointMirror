using System.Threading;
using System.Threading.Tasks;
using Microsoft.SharePoint.Client;
using SharePointMirror.Services;

namespace SharePointMirror
{
    /// <summary>
    /// Orchestrates SharePoint sync: authentication, folder traversal, and file handling are delegated to specialized services.
    /// </summary>
    public class SharePointService
    {
        private readonly IAuthContextFactory _authFactory;
        private readonly IFolderProcessor _folderProcessor;

        public SharePointService(
            IAuthContextFactory authFactory,
            IFolderProcessor folderProcessor)
        {
            _authFactory = authFactory;
            _folderProcessor = folderProcessor;
        }

        /// <summary>
        /// Starts the synchronization process.
        /// </summary>
        public Task ProcessAsync(CancellationToken token)
        {
            // Obtain authenticated ClientContext
            ClientContext ctx = _authFactory.CreateContext();

            // Delegate traversal and file operations to FolderProcessor
            return _folderProcessor.ProcessFolderAsync(ctx, token);
        }
    }
}

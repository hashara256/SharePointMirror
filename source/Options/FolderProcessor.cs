// Services/FolderProcessor.cs
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SharePoint.Client;
using SharePointMirror.Options;

namespace SharePointMirror.Services
{
    /// <summary>
    /// Processes SharePoint folders recursively and handles file download, hash verification, and optional deletion.
    /// </summary>
    public class FolderProcessor : IFolderProcessor
    {
        private readonly TrackingOptions _track;
        private readonly SharePointOptions _sp;
        private readonly ILogger<FolderProcessor> _log;

        public FolderProcessor(
            IOptions<TrackingOptions> track,
            IOptions<SharePointOptions> sp,
            ILogger<FolderProcessor> log)
        {
            _track = track.Value;
            _sp = sp.Value;
            _log = log;
        }

        public Task ProcessFolderAsync(ClientContext ctx, CancellationToken token)
        {
            // Load Web.ServerRelativeUrl
            ctx.Load(ctx.Web, w => w.ServerRelativeUrl);
            ctx.ExecuteQuery();

            // Construct the full root URL
            string webRelative = ctx.Web.ServerRelativeUrl.TrimEnd('/');
            string libRoot = _sp.LibraryRoot.StartsWith("/") ? _sp.LibraryRoot : "/" + _sp.LibraryRoot;
            string rootUrl = webRelative + libRoot;

            _log.LogDebug("Starting traversal at {RootUrl}", rootUrl);
            Traverse(ctx, rootUrl);
            return Task.CompletedTask;
        }

        private void Traverse(ClientContext ctx, string url)
        {
            _log.LogDebug("Loading folder: {Url}", url);
            var folder = ctx.Web.GetFolderByServerRelativeUrl(url);
            ctx.Load(folder.Files);
            ctx.Load(folder.Folders);
            ctx.ExecuteQuery();

            _log.LogDebug("Folder {Url} contains {FileCount} files and {FolderCount} subfolders", url, folder.Files.Count, folder.Folders.Count);

            foreach (var spFile in folder.Files.Where(f => f.Name.StartsWith(_track.FilePrefix, StringComparison.OrdinalIgnoreCase)))
            {
                _log.LogInformation("Processing file {FileName}", spFile.Name);
                ProcessFile(ctx, spFile);
            }

            foreach (var sub in folder.Folders.Where(f => _track.IgnoreFolders == null || !_track.IgnoreFolders.Contains(f.Name)))
            {
                Traverse(ctx, sub.ServerRelativeUrl);
            }
        }

        private void ProcessFile(ClientContext ctx, Microsoft.SharePoint.Client.File spFile)
        {
            try
            {
                string relative = spFile.ServerRelativeUrl.Replace(ctx.Web.ServerRelativeUrl, string.Empty).TrimStart('/');
                string localPath = Path.Combine(_track.LocalRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? throw new InvalidOperationException());

                var fileInfo = spFile.OpenBinaryStream();
                ctx.ExecuteQuery();
                using var ms = new MemoryStream();
                fileInfo.Value.CopyTo(ms);
                var data = ms.ToArray();

                System.IO.File.WriteAllBytes(localPath, data);
                _log.LogInformation("Downloaded {FileName} to {LocalPath}", spFile.Name, localPath);

                if (_track.VerifyHash)
                {
                    var h1 = SHA256.HashData(data);
                    var h2 = SHA256.HashData(System.IO.File.ReadAllBytes(localPath));
                    if (h1.SequenceEqual(h2) && _track.DeleteIfMatch)
                    {
                        spFile.DeleteObject();
                        ctx.ExecuteQuery();
                        _log.LogInformation("Deleted original {FileName}", spFile.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing {FileName}", spFile.Name);
            }
        }
    }
}

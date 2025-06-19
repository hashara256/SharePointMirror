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
            bool hashMatches = true;
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
                    hashMatches = VerifyFileHash(data, localPath);
                    _log.LogInformation("Hash verification for {FileName}: {Result}", spFile.Name, hashMatches ? "MATCH" : "MISMATCH");
                }

                if (_track.DeleteIfMatch)
                {
                    // Move to DoneFolder or ErrorFolder in LibraryRoot
                    string targetFolder = hashMatches ? _track.DoneFolder : _track.ErrorFolder;
                    string destUrl = GetTargetUrl(spFile.ServerRelativeUrl, targetFolder);

                    spFile.MoveTo(destUrl, MoveOperations.Overwrite);
                    ctx.ExecuteQuery();
                    _log.LogInformation("Moved {FileName} to {TargetFolder}", spFile.Name, targetFolder);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing {FileName}", spFile.Name);

                // On error, move to ErrorFolder if configured
                if (_track.DeleteIfMatch && !string.IsNullOrEmpty(_track.ErrorFolder))
                {
                    try
                    {
                        string destUrl = GetTargetUrl(spFile.ServerRelativeUrl, _track.ErrorFolder);
                        spFile.MoveTo(destUrl, MoveOperations.Overwrite);
                        ctx.ExecuteQuery();
                        _log.LogInformation("Moved errored {FileName} to {ErrorFolder}", spFile.Name, _track.ErrorFolder);
                    }
                    catch (Exception moveEx)
                    {
                        _log.LogError(moveEx, "Failed to move errored {FileName} to {ErrorFolder}", spFile.Name, _track.ErrorFolder);
                    }
                }
            }
        }

        private string GetTargetUrl(string originalUrl, string targetFolder)
        {
            var fileName = Path.GetFileName(originalUrl);
            var libRoot = _sp.LibraryRoot.TrimEnd('/');
            var destUrl = $"{libRoot}/{targetFolder}/{fileName}";
            if (!destUrl.StartsWith("/")) destUrl = "/" + destUrl;
            return destUrl;
        }

        private bool VerifyFileHash(byte[] originalData, string localPath)
        {
            var h1 = SHA256.HashData(originalData);
            var h2 = SHA256.HashData(System.IO.File.ReadAllBytes(localPath));
            return h1.SequenceEqual(h2);
        }
    }
}

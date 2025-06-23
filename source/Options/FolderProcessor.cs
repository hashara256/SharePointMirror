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
    /// Recursively processes SharePoint folders, handling file download, hash verification, and post-processing actions.
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
            // Load the server-relative URL of the SharePoint web
            ctx.Load(ctx.Web, w => w.ServerRelativeUrl);
            ctx.ExecuteQuery();

            // Build the root URL for the library
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

            // Build ignore list for folders
            var ignoreFolders = (_track.IgnoreFolders ?? new List<string>()).ToList();
            if (!string.IsNullOrEmpty(_track.DoneFolder) && !ignoreFolders.Contains(_track.DoneFolder))
                ignoreFolders.Add(_track.DoneFolder);
            if (!string.IsNullOrEmpty(_track.ErrorFolder) && !ignoreFolders.Contains(_track.ErrorFolder))
                ignoreFolders.Add(_track.ErrorFolder);

            foreach (var spFile in folder.Files.Where(f => f.Name.StartsWith(_track.FilePrefix, StringComparison.OrdinalIgnoreCase)))
            {
                _log.LogInformation("Processing file {FileName}", spFile.Name);
                ProcessFile(ctx, spFile);
            }

            foreach (var sub in folder.Folders.Where(f =>
                !string.Equals(f.Name, _track.DoneFolder, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(f.Name, _track.ErrorFolder, StringComparison.OrdinalIgnoreCase) &&
                (_track.IgnoreFolders == null || !_track.IgnoreFolders.Contains(f.Name))))
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
                
                // trim relative path. removing the library root from it
                if (_sp.LibraryRoot.StartsWith("/"))
                    relative = relative.Substring(_sp.LibraryRoot.Length).TrimStart('/');
                else
                    relative = relative.Substring(_sp.LibraryRoot.Length + 1).TrimStart('/');



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

                switch (_track.ActionAfterProcessed)
                {
                    case ActionAfterProcessed.Move:
                        {
                            // Move file to Done or Error folder based on hash result
                            string targetFolder = hashMatches ? _track.DoneFolder : _track.ErrorFolder;
                            string destUrl = GetTargetUrl(spFile.ServerRelativeUrl, targetFolder, ctx.Web.ServerRelativeUrl);

                            var destFolderUrl = destUrl.Substring(0, destUrl.LastIndexOf('/'));
                            EnsureFolderExists(ctx, destFolderUrl);

                            spFile.MoveTo(destUrl, MoveOperations.Overwrite);
                            ctx.ExecuteQuery();
                            _log.LogInformation("Moved {FileName} to {TargetFolder}", spFile.Name, targetFolder);
                            break;
                        }
                    case ActionAfterProcessed.Delete:
                        {
                            // Delete file from SharePoint after processing
                            spFile.DeleteObject();
                            ctx.ExecuteQuery();
                            _log.LogInformation("Deleted {FileName} from SharePoint after processing", spFile.Name);
                            break;
                        }
                    case ActionAfterProcessed.None:
                        {
                            _log.LogInformation("No action taken for {FileName} after processing", spFile.Name);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing {FileName}", spFile.Name);

                // On error, move file to ErrorFolder if configured and action is Move
                if (_track.ActionAfterProcessed == ActionAfterProcessed.Move && !string.IsNullOrEmpty(_track.ErrorFolder))
                {
                    try
                    {
                        string destUrl = GetTargetUrl(spFile.ServerRelativeUrl, _track.ErrorFolder, ctx.Web.ServerRelativeUrl);
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

        private string GetTargetUrl(string originalUrl, string targetFolder, string webServerRelativeUrl)
        {
            var fileName = Path.GetFileName(originalUrl);
            // Compute the relative path inside the library
            var libRoot = _sp.LibraryRoot.TrimEnd('/');
            var webRoot = webServerRelativeUrl.TrimEnd('/');

            var relativePath = originalUrl;
            if (relativePath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Substring(webRoot.Length);
            if (relativePath.StartsWith(libRoot, StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Substring(libRoot.Length);

            relativePath = relativePath.TrimStart('/');

            var parentDir = Path.GetDirectoryName(relativePath.Replace('\\', '/'))?.Replace("\\", "/");
            // Compose new folder path for Done or Error folder
            var targetDir = string.IsNullOrEmpty(parentDir)
                ? $"{libRoot}/{targetFolder}"
                : $"{libRoot}/{parentDir}/{targetFolder}";

            var destUrl = $"{webRoot}{(targetDir.StartsWith("/") ? "" : "/")}{targetDir}/{fileName}";
            return destUrl;
        }

        private bool VerifyFileHash(byte[] originalData, string localPath)
        {
            var h1 = SHA256.HashData(originalData);
            var h2 = SHA256.HashData(System.IO.File.ReadAllBytes(localPath));
            return h1.SequenceEqual(h2);
        }

        private void EnsureFolderExists(ClientContext ctx, string folderServerRelativeUrl)
        {
            var folder = ctx.Web.GetFolderByServerRelativeUrl(folderServerRelativeUrl);
            ctx.Load(folder, f => f.Exists);
            try
            {
                ctx.ExecuteQuery();
                if (!folder.Exists)
                {
                    // Create the folder if it does not exist
                    var parentUrl = folderServerRelativeUrl.Substring(0, folderServerRelativeUrl.LastIndexOf('/'));
                    var folderName = folderServerRelativeUrl.Substring(folderServerRelativeUrl.LastIndexOf('/') + 1);
                    var parentFolder = ctx.Web.GetFolderByServerRelativeUrl(parentUrl);
                    parentFolder.Folders.Add(folderName);
                    ctx.ExecuteQuery();
                }
            }
            catch (ServerException ex) when (ex.ServerErrorTypeName == "System.IO.FileNotFoundException")
            {
                // Folder does not exist, create it
                var parentUrl = folderServerRelativeUrl.Substring(0, folderServerRelativeUrl.LastIndexOf('/'));
                var folderName = folderServerRelativeUrl.Substring(folderServerRelativeUrl.LastIndexOf('/') + 1);
                var parentFolder = ctx.Web.GetFolderByServerRelativeUrl(parentUrl);
                parentFolder.Folders.Add(folderName);
                ctx.ExecuteQuery();
            }
        }
    }
}

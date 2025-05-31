namespace SharePointMirror.Options;

public class TrackingOptions
{
    public string FilePrefix { get; set; } = default!;
    public string LocalRootPath { get; set; } = default!;
    public bool VerifyHash { get; set; } = true;
    public bool DeleteIfMatch { get; set; } = false;
    public List<string>? IgnoreFolders { get; set; }
    public int PollIntervalSeconds { get; set; } = 300;
}

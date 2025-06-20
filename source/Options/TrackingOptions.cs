namespace SharePointMirror.Options;

public class TrackingOptions
{
    public string FilePrefix { get; set; } = default!;
    public string LocalRootPath { get; set; } = default!;
    public bool VerifyHash { get; set; } = true;
    public ActionAfterProcessed ActionAfterProcessed { get; set; } = ActionAfterProcessed.Move;
    public List<string>? IgnoreFolders { get; set; }
    public int PollIntervalSeconds { get; set; } = 300;
    public string DoneFolder { get; set; } = "_done";
    public string ErrorFolder { get; set; } = "_error";
}

public enum ActionAfterProcessed
{
    None,
    Move,
    Delete
}

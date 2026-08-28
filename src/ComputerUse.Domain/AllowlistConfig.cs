namespace ComputerUse.Domain;

public sealed class AllowlistConfig
{
    public List<string> AllowedHosts { get; set; } = [];
    public List<int> AllowedPorts { get; set; } = [];
    public List<string> AllowedPathPrefixes { get; set; } = [];
    public List<string> AllowedActions { get; set; } = [];
    public List<string> ProhibitedPathPrefixes { get; set; } = [];
}

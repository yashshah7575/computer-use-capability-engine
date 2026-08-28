using System.Text.Json;
using ComputerUse.Domain;

namespace ComputerUse.Agent;

internal sealed class DiscoveryEvidenceLog
{
    private readonly string _path;
    private readonly object _gate = new();

    public DiscoveryEvidenceLog(string evidenceDir)
    {
        Directory.CreateDirectory(evidenceDir);
        _path = Path.Combine(evidenceDir, Constants.PathName.DiscoveryLog);
    }

    public void Write(int step, string eventName, IReadOnlyDictionary<string, string?>? fields = null)
    {
        var payload = new Dictionary<string, string?>
        {
            ["step"] = step.ToString(),
            ["event"] = eventName
        };
        if (fields is not null)
        {
            foreach (var kv in fields)
                payload[kv.Key] = kv.Value is null ? null : Redaction.Redact(kv.Value);
        }

        var line = JsonSerializer.Serialize(payload);
        lock (_gate)
            File.AppendAllText(_path, line + Environment.NewLine);
    }
}

using System.Text.Json;

namespace ToolTikTokV12.Services;

/// <summary>Single source of truth for Manager/Worker IPC identity and handshake.</summary>
public static class WorkerIpcProtocol
{
    public const int ProtocolVersion = 1;
    public const string PipePrefix = "ToolTikTokV13_";

    public static string BuildPipeName(string profileName)
    {
        var profile = (profileName ?? "").Trim();
        if (profile.Length == 0) throw new ArgumentException("Profile name is required.", nameof(profileName));
        return PipePrefix + profile;
    }

    public static string BuildHandshake(string workerVersion, string profile)
        => JsonSerializer.Serialize(new WorkerIpcHandshake
        {
            WorkerVersion = workerVersion,
            ProtocolVersion = ProtocolVersion,
            Profile = profile
        });

    public static WorkerIpcHandshake? ParseHandshake(string json)
        => JsonSerializer.Deserialize<WorkerIpcHandshake>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
}

public sealed class WorkerIpcHandshake
{
    public string WorkerVersion { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string Profile { get; set; } = "";
}

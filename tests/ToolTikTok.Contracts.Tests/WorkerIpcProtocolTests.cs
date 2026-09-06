using ToolTikTokV12.Services;
using Xunit;

namespace ToolTikTok.Contracts.Tests;

public sealed class WorkerIpcProtocolTests
{
    [Fact]
    public void Handshake_RoundTripsWithoutChangingProtocol()
    {
        var json = WorkerIpcProtocol.BuildHandshake("13.7.9", "acc01");
        var result = WorkerIpcProtocol.ParseHandshake(json);

        Assert.NotNull(result);
        Assert.Equal(WorkerIpcProtocol.ProtocolVersion, result.ProtocolVersion);
        Assert.Equal("13.7.9", result.WorkerVersion);
        Assert.Equal("acc01", result.Profile);
    }

    [Fact]
    public void PipeName_PreservesCompatibilityPrefix()
        => Assert.Equal("ToolTikTokV13_acc01", WorkerIpcProtocol.BuildPipeName(" acc01 "));
}

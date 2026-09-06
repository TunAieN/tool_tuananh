using ToolTikTokManagerV13;
using ToolTikTokV11;
using Xunit;

namespace ToolTikTok.IntegrationTests;

public sealed class CompatibilityTests
{
    [Fact]
    public void ManagerAssemblyName_RemainsInstallerCompatible()
        => Assert.Equal("ToolTikTokManagerV13", typeof(ManagerForm).Assembly.GetName().Name);

    [Fact]
    public void WorkerMainForm_RemainsAvailableFromAutomationAssembly()
        => Assert.Equal("ToolTikTokV11.MainForm", typeof(MainForm).FullName);
}

using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public class AgentMetadataTests
{
    [Fact]
    public void ServiceNameIdentifiesWindowsAgent()
    {
        Assert.Equal("PhotoBIZ.WindowsAgent", AgentMetadata.ServiceName);
    }

    [Fact]
    public void RuntimeKindIdentifiesControlCenter()
    {
        Assert.Equal("ControlCenter", AgentMetadata.RuntimeKind);
        Assert.False(string.IsNullOrWhiteSpace(AgentMetadata.Version));
    }
}

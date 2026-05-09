using PhotoBIZ.WindowsAgent;

namespace PhotoBIZ.WindowsAgent.Tests;

public class AgentMetadataTests
{
    [Fact]
    public void ServiceNameIdentifiesWindowsAgent()
    {
        Assert.Equal("PhotoBIZ.WindowsAgent", AgentMetadata.ServiceName);
    }
}

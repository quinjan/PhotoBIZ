using PhotoBIZ.Worker;

namespace PhotoBIZ.Worker.Tests;

public class WorkerMetadataTests
{
    [Fact]
    public void ServiceNameIdentifiesBackendWorker()
    {
        Assert.Equal("PhotoBIZ.Worker", WorkerMetadata.ServiceName);
    }
}

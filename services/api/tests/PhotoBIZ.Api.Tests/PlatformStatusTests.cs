namespace PhotoBIZ.Api.Tests;

public class PlatformStatusTests
{
    [Fact]
    public void PlatformStatusContractUsesNet10()
    {
        var status = new
        {
            Service = "PhotoBIZ.Api",
            Runtime = "net10.0"
        };

        Assert.Equal("PhotoBIZ.Api", status.Service);
        Assert.Equal("net10.0", status.Runtime);
    }
}

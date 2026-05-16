using System.Text.Json;

namespace PhotoBIZ.WindowsAgent;

public interface IActiveLumaBoothSessionStore
{
    Task<ActiveLumaBoothSession?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed class FileActiveLumaBoothSessionStore : IActiveLumaBoothSessionStore
{
    private readonly string filePath;

    public FileActiveLumaBoothSessionStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PhotoBIZ",
            "agent");
        filePath = Path.Combine(root, "active-session.json");
    }

    public async Task<ActiveLumaBoothSession?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<ActiveLumaBoothSession>(stream, cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(ActiveLumaBoothSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, session, cancellationToken: cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}

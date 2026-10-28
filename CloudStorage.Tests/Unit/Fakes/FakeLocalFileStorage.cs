using CloudStorage.API.Services.FileSystem;

namespace CloudStorage.Tests.Unit.Fakes;

internal sealed class FakeLocalFileStorage : ILocalFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public Exception? SaveException { get; set; }

    public int DeleteCalls { get; private set; }

    public string? LastSavedKey { get; private set; }

    public string? LastDeletedKey { get; private set; }

    public long LastSavedLength { get; private set; }

    public async Task Save(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        if (SaveException is not null) throw SaveException;

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[storageKey] = buffer.ToArray();
        LastSavedKey = storageKey;
        LastSavedLength = buffer.Length;
    }

    public Stream? OpenRead(string storageKey)
    {
        return Files.TryGetValue(storageKey, out var data) ? new MemoryStream(data) : null;
    }

    public bool Exists(string storageKey)
    {
        return Files.ContainsKey(storageKey);
    }

    public void Delete(string storageKey)
    {
        DeleteCalls++;
        LastDeletedKey = storageKey;
        Files.Remove(storageKey);
    }
}
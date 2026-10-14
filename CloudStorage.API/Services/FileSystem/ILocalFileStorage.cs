namespace CloudStorage.API.Services.FileSystem;

public interface ILocalFileStorage
{
    Task Save(string storageKey, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenRead(string storageKey);

    bool Exists(string storageKey);

    void Delete(string storageKey);
}
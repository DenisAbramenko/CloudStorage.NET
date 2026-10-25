namespace CloudStorage.API.Services.FileSystem;

public interface ILocalFileStorage
{
    Task Save(string storageKey, Stream content, CancellationToken cancellationToken);

    Stream? OpenRead(string storageKey);

    bool Exists(string storageKey);

    void Delete(string storageKey);
}
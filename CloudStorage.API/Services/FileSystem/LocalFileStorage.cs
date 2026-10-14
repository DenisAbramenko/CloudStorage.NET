namespace CloudStorage.API.Services.FileSystem;

public class LocalFileStorage : ILocalFileStorage
{
    private readonly string _uploadDirectory;


    public LocalFileStorage(IWebHostEnvironment environment)
    {
        _uploadDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "Uploads");
        Directory.CreateDirectory(_uploadDirectory);
    }


    public async Task Save(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(storageKey);
        await using var fileStream = new FileStream(filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Stream?> OpenRead(string storageKey)
    {
        var filePath = GetFilePath(storageKey);
        var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult<Stream?>(fileStream);
    }

    public bool Exists(string storageKey)
    {
        return File.Exists(GetFilePath(storageKey));
    }

    public void Delete(string storageKey)
    {
        File.Delete(GetFilePath(storageKey));
    }

    private string GetFilePath(string storageKey)
    {
        var filePath = Path.Combine(_uploadDirectory, storageKey);
        return filePath;
    }
}
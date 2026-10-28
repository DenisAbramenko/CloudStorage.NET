namespace CloudStorage.API.Services.FileSystem;

public class LocalFileStorage : ILocalFileStorage
{
    private readonly string _uploadDirectory;

    public LocalFileStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredRoot = configuration["Storage:RootPath"];
        _uploadDirectory = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "Uploads")
            : Path.GetFullPath(configuredRoot, environment.ContentRootPath);
        Directory.CreateDirectory(_uploadDirectory);
    }

    public async Task Save(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(storageKey);
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            81920,
            true);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Stream? OpenRead(string storageKey)
    {
        var filePath = GetFilePath(storageKey);
        var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            true);
        return fileStream;
    }

    public bool Exists(string storageKey)
    {
        return File.Exists(GetFilePath(storageKey));
    }

    public void Delete(string storageKey)
    {
        var filePath = GetFilePath(storageKey);
        File.Delete(filePath);
    }

    private string GetFilePath(string storageKey)
    {
        var filePath = Path.Combine(_uploadDirectory, storageKey);
        return filePath;
    }
}
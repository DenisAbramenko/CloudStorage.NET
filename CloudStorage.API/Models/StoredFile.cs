namespace CloudStorage.API.Models;

public class StoredFile
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public required string UserId { get; set; } = string.Empty;
}
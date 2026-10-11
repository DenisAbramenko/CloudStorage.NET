namespace CloudStorage.API.DTOs;

public class StoredFileDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
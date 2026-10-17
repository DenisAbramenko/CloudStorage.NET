namespace CloudStorage.API.DTOs;

public sealed class FileDownloadResult
{
    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }
}
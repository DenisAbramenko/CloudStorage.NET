using Microsoft.AspNetCore.Http;

namespace CloudStorage.Tests.Unit.Fakes;

internal sealed class FakeFormFile : IFormFile
{
    public FakeFormFile(string fileName, string? contentType, int length)
    {
        FileName = fileName;
        ContentType = contentType ?? string.Empty;
        Length = length;
    }

    public string ContentType { get; }

    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";

    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

    public long Length { get; }

    public string Name => "file";

    public string FileName { get; }

    public Stream OpenReadStream()
    {
        return new MemoryStream(new byte[Length]);
    }

    public void CopyTo(Stream target)
    {
        target.Write(new byte[Length]);
    }

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        return target.WriteAsync(new byte[Length], cancellationToken).AsTask();
    }
}
using CloudStorage.API.DTOs;
using CloudStorage.API.Models;

namespace CloudStorage.API.Services.Mapping;

public class StoredFileMapper : IStoredFileMapper
{
    public StoredFileDto ToDto(StoredFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new StoredFileDto
        {
            Id = file.Id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Size,
            CreatedAtUtc = file.CreatedAtUtc
        };
    }

    public List<StoredFileDto> ToDtoList(IEnumerable<StoredFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return files.Select(ToDto).ToList();
    }
}
using CloudStorage.API.DTOs;
using CloudStorage.API.Models;
using CloudStorage.API.Services.Mapping;
using Xunit;

namespace CloudStorage.Tests.Unit;

public class StoredFileMapperTests
{
    private readonly StoredFileMapper _mapper = new();

    private static StoredFile CreateFile()
    {
        return new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = "report.pdf",
            StorageKey = "1b4e28ba2f3d4c0e9a7f6d5c4b3a2918",
            ContentType = "application/pdf",
            Size = 1024,
            CreatedAtUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            UserId = "user-1"
        };
    }

    [Fact]
    public void ToDto_MapsAllFields()
    {
        var file = CreateFile();

        var dto = _mapper.ToDto(file);

        Assert.Equal(file.Id, dto.Id);
        Assert.Equal(file.FileName, dto.FileName);
        Assert.Equal(file.ContentType, dto.ContentType);
        Assert.Equal(file.Size, dto.Size);
        Assert.Equal(file.CreatedAtUtc, dto.CreatedAtUtc);
    }

    [Fact]
    public void ToDto_DoesNotExposeStorageKeyOrUserId()
    {
        var dto = _mapper.ToDto(CreateFile());

        Assert.Null(typeof(StoredFileDto).GetProperty(nameof(StoredFile.StorageKey)));
        Assert.Null(typeof(StoredFileDto).GetProperty(nameof(StoredFile.UserId)));
    }

    [Fact]
    public void ToDtoList_MapsEveryItemInOrder()
    {
        var files = new[] { CreateFile(), CreateFile(), CreateFile() };

        var result = _mapper.ToDtoList(files);

        Assert.Equal(files.Length, result.Count);
        Assert.Equal(files.Select(file => file.Id), result.Select(dto => dto.Id));
    }

    [Fact]
    public void ToDto_NullFile_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _mapper.ToDto(null!));
    }
}

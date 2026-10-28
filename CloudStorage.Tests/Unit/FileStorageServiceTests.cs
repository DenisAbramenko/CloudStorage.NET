using CloudStorage.API.Models;
using CloudStorage.API.Services;
using CloudStorage.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CloudStorage.Tests.Unit;

public class FileStorageServiceTests
{
    private readonly FakeStoredFileRepository _repository = new();
    private readonly FakeLocalFileStorage _storage = new();
    private readonly FileStorageService _sut;

    public FileStorageServiceTests()
    {
        _sut = new FileStorageService(_repository, _storage);
    }

    private static IFormFile CreateFile(
        string fileName = "note.txt",
        string? contentType = "text/plain",
        int length = 5)
    {
        return new FakeFormFile(fileName, contentType, length);
    }

    private static StoredFile CreateStoredFile(string userId = "user-1", string storageKey = "storage-key")
    {
        return new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = "note.txt",
            StorageKey = storageKey,
            ContentType = "text/plain",
            Size = 5,
            CreatedAtUtc = DateTime.UtcNow,
            UserId = userId
        };
    }

    // Сценарий 3: метаданные сохраняются после загрузки.
    [Fact]
    public async Task UploadAsync_SavesMetadataOfUploadedFile()
    {
        var file = CreateFile(@"C:\temp\report.pdf", "application/pdf", 42);

        var result = await _sut.UploadAsync(file, "user-1", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("report.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(42, result.Size);
        Assert.Equal("user-1", result.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.StorageKey));
        Assert.Equal(1, _repository.AddCalls);
        Assert.Equal(result.StorageKey, _storage.LastSavedKey);
        Assert.Equal(42, _storage.LastSavedLength);
    }

    [Fact]
    public async Task UploadAsync_UsesDefaultContentType_WhenClientSendsEmptyOne()
    {
        var file = CreateFile(contentType: null);

        var result = await _sut.UploadAsync(file, "user-1", CancellationToken.None);

        Assert.Equal("application/octet-stream", result.ContentType);
    }

    [Fact]
    public async Task UploadAsync_NullFile_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.UploadAsync(null, "user-1", CancellationToken.None));
        Assert.Equal(0, _repository.AddCalls);
    }

    // Сценарий 4: невалидный/чужой идентификатор не приводит к необработанному исключению.
    [Fact]
    public async Task DownloadAsync_UnknownId_ReturnsNullWithoutThrowing()
    {
        _repository.FileToReturn = null;

        var result = await _sut.DownloadAsync("user-1", Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadAsync_QueriesRepositoryByUserIdAndId()
    {
        var id = Guid.NewGuid();
        _repository.FileToReturn = null;

        await _sut.DownloadAsync("user-1", id, CancellationToken.None);

        Assert.Equal("user-1", _repository.LastUserId);
        Assert.Equal(id, _repository.LastId);
    }

    [Fact]
    public async Task DownloadAsync_ReturnsContentOfOwnFile()
    {
        var storedFile = CreateStoredFile();
        _repository.FileToReturn = storedFile;
        _storage.Files[storedFile.StorageKey] = [1, 2, 3, 4, 5];

        var result = await _sut.DownloadAsync("user-1", storedFile.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(storedFile.FileName, result!.FileName);
        Assert.Equal(storedFile.ContentType, result.ContentType);
        using var reader = new MemoryStream();
        await result.Content.CopyToAsync(reader);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, reader.ToArray());
    }

    // Сценарий 5: нельзя скачать отсутствующий файл.
    [Fact]
    public async Task DownloadAsync_WhenRecordExistsButFileMissingOnDisk_ThrowsFileNotFound()
    {
        _repository.FileToReturn = CreateStoredFile(storageKey: "missing-key");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _sut.DownloadAsync("user-1", Guid.NewGuid(), CancellationToken.None));
    }

    // Сценарий 6: удаление несуществующего файла возвращает ожидаемую ошибку.
    [Fact]
    public async Task DeleteById_UnknownId_ReturnsFalseAndKeepsStorageIntact()
    {
        _repository.FileToReturn = null;

        var deleted = await _sut.DeleteById("user-1", Guid.NewGuid(), CancellationToken.None);

        Assert.False(deleted);
        Assert.Equal(0, _storage.DeleteCalls);
        Assert.Equal(0, _repository.DeleteCalls);
    }

    [Fact]
    public async Task DeleteById_OwnFile_DeletesFileAndRecord()
    {
        var storedFile = CreateStoredFile();
        _repository.FileToReturn = storedFile;
        _storage.Files[storedFile.StorageKey] = [1];

        var deleted = await _sut.DeleteById("user-1", storedFile.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Equal(1, _storage.DeleteCalls);
        Assert.Equal(storedFile.StorageKey, _storage.LastDeletedKey);
        Assert.Equal(1, _repository.DeleteCalls);
    }

    // Сценарий 7: ошибка файлового хранилища обрабатывается корректно.
    [Fact]
    public async Task UploadAsync_WhenStorageFails_DoesNotPersistMetadata()
    {
        _storage.SaveException = new IOException("disk is full");

        await Assert.ThrowsAsync<IOException>(
            () => _sut.UploadAsync(CreateFile(), "user-1", CancellationToken.None));

        Assert.Equal(0, _repository.AddCalls);
    }

    [Fact]
    public async Task UploadAsync_WhenDatabaseFails_RemovesAlreadyStoredFile()
    {
        _repository.AddException = new InvalidOperationException("database is down");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UploadAsync(CreateFile(), "user-1", CancellationToken.None));

        Assert.Equal(1, _storage.DeleteCalls);
        Assert.Equal(_storage.LastSavedKey, _storage.LastDeletedKey);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task GetAllFiles_PassesUserIdToRepository()
    {
        _repository.FilesToReturn = [CreateStoredFile()];

        var files = await _sut.GetAllFiles("user-1", CancellationToken.None);

        Assert.Single(files);
        Assert.Equal("user-1", _repository.LastUserId);
    }
}
using CloudStorage.API.DTOs;
using CloudStorage.API.Models;
using CloudStorage.API.Services;
using Microsoft.AspNetCore.Http;

namespace CloudStorage.Tests.Unit.Fakes;

internal sealed class FakeFileStorageService : IFileStorageService
{
    public StoredFile? UploadResult { get; set; }

    public List<StoredFile> FilesToReturn { get; set; } = [];

    public FileDownloadResult? DownloadResult { get; set; }

    public bool DeleteResult { get; set; }

    public int UploadCalls { get; private set; }

    public string? LastUserId { get; private set; }

    public Task<StoredFile> UploadAsync(IFormFile? file, string userId, CancellationToken cancellationToken)
    {
        UploadCalls++;
        LastUserId = userId;
        return Task.FromResult(UploadResult ?? throw new InvalidOperationException("UploadResult не задан"));
    }

    public Task<List<StoredFile>> GetAllFiles(string userId, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        return Task.FromResult(FilesToReturn);
    }

    public Task<FileDownloadResult?> DownloadAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        return Task.FromResult(DownloadResult);
    }

    public Task<bool> DeleteById(string userId, Guid id, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        return Task.FromResult(DeleteResult);
    }
}
using CloudStorage.API.DTOs;
using CloudStorage.API.Models;
using CloudStorage.API.Repositories;

namespace CloudStorage.Tests.Unit.Fakes;

internal sealed class FakeStoredFileRepository : IStoredFileRepository
{
    public StoredFile? FileToReturn { get; set; }

    public List<StoredFile> FilesToReturn { get; set; } = [];

    public Exception? AddException { get; set; }

    public int AddCalls { get; private set; }

    public int DeleteCalls { get; private set; }

    public string? LastUserId { get; private set; }

    public Guid? LastId { get; private set; }

    public FilesQueryParameters? LastQuery { get; private set; }

    public Task AddAsync(StoredFile file, CancellationToken cancellationToken)
    {
        AddCalls++;
        if (AddException is not null) throw AddException;
        return Task.CompletedTask;
    }

    public Task<(List<StoredFile> Items, int Total)> GetPageAsync(
        string userId,
        FilesQueryParameters query,
        CancellationToken cancellationToken)
    {
        LastUserId = userId;
        LastQuery = query;
        return Task.FromResult((FilesToReturn, FilesToReturn.Count));
    }

    public Task<List<StoredFile>> GetAllAsync(string userId, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        return Task.FromResult(FilesToReturn);
    }

    public Task<StoredFile?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        LastUserId = userId;
        LastId = id;
        return Task.FromResult(FileToReturn);
    }

    public Task Delete(StoredFile file, CancellationToken cancellationToken)
    {
        DeleteCalls++;
        return Task.CompletedTask;
    }
}
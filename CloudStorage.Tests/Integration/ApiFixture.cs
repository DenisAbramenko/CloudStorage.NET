using CloudStorage.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CloudStorage.Tests.Integration;

public sealed class ApiFixture : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public HttpClient CreateClient()
    {
        return _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        TestDatabase.EnsureDatabaseExists();
        _factory = new ApiFactory();

        try
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
            await dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE \"StoredFiles\", \"AspNetUsers\" CASCADE");
        }
        catch
        {
            await _factory.DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api-integration";
}
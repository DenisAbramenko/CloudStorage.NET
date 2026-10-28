using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CloudStorage.Tests.Integration;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "cloudstorage-tests-" + Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-tests-signing-key-0123456789abcdef");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", TestDatabase.ConnectionString);
        Environment.SetEnvironmentVariable("Storage__RootPath", _storageRoot);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing) DeleteStorageRoot();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteStorageRoot();
    }

    private void DeleteStorageRoot()
    {
        if (!Directory.Exists(_storageRoot)) return;

        try
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
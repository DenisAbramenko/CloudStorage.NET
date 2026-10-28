using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CloudStorage.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CloudStorage.Tests.Integration;

[Collection(ApiCollection.Name)]
public class ApiIntegrationTests
{
    private const string Password = "Passw0rd!23";

    private readonly ApiFixture _fixture;

    public ApiIntegrationTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RegisterAndLogin_ReturnsAccessToken()
    {
        var email = NewEmail("register");
        using var client = _fixture.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var token = await ReadAccessTokenAsync(loginResponse);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithProblemDetails()
    {
        var email = NewEmail("wrong-password");
        using var client = _fixture.CreateClient();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong-Pass!23" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401WithProblemDetails()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/files");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(401, problem!.Status);
    }

    [Fact]
    public async Task UploadAndDownload_ReturnsUploadedContent()
    {
        using var client = await CreateAuthorizedClientAsync("upload-download");
        var content = "содержимое тестового файла";

        var uploaded = await UploadAsync(client, "note.txt", content);

        Assert.NotEqual(Guid.Empty, uploaded.Id);
        Assert.Equal("note.txt", uploaded.FileName);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), uploaded.Size);

        var downloadResponse = await client.GetAsync($"/api/files/{uploaded.Id}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(content, await downloadResponse.Content.ReadAsStringAsync());

        var listResponse = await client.GetAsync("/api/files");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var files = await listResponse.Content.ReadFromJsonAsync<List<StoredFileDto>>();
        Assert.Contains(files!, f => f.Id == uploaded.Id);
    }

    [Fact]
    public async Task FilesOfTwoUsers_AreIsolated()
    {
        using var alice = await CreateAuthorizedClientAsync("alice");
        using var bob = await CreateAuthorizedClientAsync("bob");

        var uploadedByAlice = await UploadAsync(alice, "alice.txt", "данные Алисы");

        var bobList = await bob.GetAsync("/api/files");
        Assert.Equal(HttpStatusCode.OK, bobList.StatusCode);
        Assert.Empty((await bobList.Content.ReadFromJsonAsync<List<StoredFileDto>>())!);

        var bobDownload = await bob.GetAsync($"/api/files/{uploadedByAlice.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, bobDownload.StatusCode);

        var bobDelete = await bob.DeleteAsync($"/api/files/{uploadedByAlice.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bobDelete.StatusCode);

        var aliceList = await alice.GetAsync("/api/files");
        var aliceFiles = await aliceList.Content.ReadFromJsonAsync<List<StoredFileDto>>();
        Assert.Single(aliceFiles!, f => f.Id == uploadedByAlice.Id);
    }

    [Fact]
    public async Task DeleteFile_Returns204AndFileBecomesUnavailable()
    {
        using var client = await CreateAuthorizedClientAsync("delete");
        var uploaded = await UploadAsync(client, "to-delete.txt", "временный файл");

        var deleteResponse = await client.DeleteAsync($"/api/files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(await deleteResponse.Content.ReadAsStringAsync());

        var downloadAfterDelete = await client.GetAsync($"/api/files/{uploaded.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, downloadAfterDelete.StatusCode);

        var secondDelete = await client.DeleteAsync($"/api/files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }

    private static string NewEmail(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}@example.com";
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("access_token").GetString()!;
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(string prefix)
    {
        var email = NewEmail(prefix);
        var client = _fixture.CreateClient();

        await RegisterAsync(client, email);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var token = await ReadAccessTokenAsync(loginResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<StoredFileDto> UploadAsync(HttpClient client, string fileName, string content)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("/api/files", multipart);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StoredFileDto>())!;
    }
}
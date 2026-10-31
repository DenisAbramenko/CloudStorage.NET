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
        var files = await listResponse.Content.ReadFromJsonAsync<PagedResult<StoredFileDto>>();
        Assert.Contains(files!.Items, f => f.Id == uploaded.Id);
    }

    [Fact]
    public async Task FilesList_IsPagedAndSorted()
    {
        using var client = await CreateAuthorizedClientAsync("paging");
        await UploadAsync(client, "a-first.txt", "1");
        await UploadAsync(client, "b-second.txt", "22");
        await UploadAsync(client, "c-third.txt", "333");

        var firstPage = await ReadPageAsync(client, "?page=1&pageSize=2&sortBy=name&sortDirection=asc");
        Assert.Equal(3, firstPage.Total);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(["a-first.txt", "b-second.txt"], firstPage.Items.Select(file => file.FileName));

        var secondPage = await ReadPageAsync(client, "?page=2&pageSize=2&sortBy=name&sortDirection=asc");
        Assert.Single(secondPage.Items);
        Assert.Equal("c-third.txt", secondPage.Items[0].FileName);

        var bySizeDescending = await ReadPageAsync(client, "?pageSize=2&sortBy=size&sortDirection=desc");
        Assert.Equal(["c-third.txt", "b-second.txt"], bySizeDescending.Items.Select(file => file.FileName));

        // Сортировка по умолчанию (createdAt desc) возвращает все файлы без дублей.
        var allFiles = await ReadPageAsync(client, "?pageSize=100");
        Assert.Equal(3, allFiles.Items.Count);
        Assert.Equal(3, allFiles.Items.Select(file => file.Id).Distinct().Count());
    }

    [Fact]
    public async Task FilesList_PageBeyondLast_Returns200WithEmptyItems()
    {
        using var client = await CreateAuthorizedClientAsync("paging-beyond");
        await UploadAsync(client, "only.txt", "1");

        var response = await client.GetAsync("/api/files?page=99&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<PagedResult<StoredFileDto>>())!;
        Assert.Equal(1, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task FilesList_InvalidPagingParameters_Return400WithProblemDetails()
    {
        using var client = await CreateAuthorizedClientAsync("paging-invalid");

        string[] invalidQueries =
        [
            "?page=0",
            "?pageSize=101",
            "?sortBy=unknown",
            "?sortDirection=sideways"
        ];

        foreach (var query in invalidQueries)
        {
            var response = await client.GetAsync("/api/files" + query);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task FilesList_DoesNotExposeInternalFields()
    {
        using var client = await CreateAuthorizedClientAsync("no-leak");
        await UploadAsync(client, "secret.txt", "content");

        var response = await client.GetAsync("/api/files");
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FilesOfTwoUsers_AreIsolated()
    {
        using var alice = await CreateAuthorizedClientAsync("alice");
        using var bob = await CreateAuthorizedClientAsync("bob");

        var uploadedByAlice = await UploadAsync(alice, "alice.txt", "данные Алисы");

        var bobList = await bob.GetAsync("/api/files");
        Assert.Equal(HttpStatusCode.OK, bobList.StatusCode);
        var bobFiles = await bobList.Content.ReadFromJsonAsync<PagedResult<StoredFileDto>>();
        Assert.Empty(bobFiles!.Items);
        Assert.Equal(0, bobFiles.Total);

        var bobDownload = await bob.GetAsync($"/api/files/{uploadedByAlice.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, bobDownload.StatusCode);

        var bobDelete = await bob.DeleteAsync($"/api/files/{uploadedByAlice.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bobDelete.StatusCode);

        var aliceList = await alice.GetAsync("/api/files");
        var aliceFiles = await aliceList.Content.ReadFromJsonAsync<PagedResult<StoredFileDto>>();
        Assert.Single(aliceFiles!.Items, f => f.Id == uploadedByAlice.Id);
        Assert.Equal(1, aliceFiles.Total);
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

    private static async Task<PagedResult<StoredFileDto>> ReadPageAsync(HttpClient client, string queryString)
    {
        var response = await client.GetAsync("/api/files" + queryString);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<StoredFileDto>>())!;
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
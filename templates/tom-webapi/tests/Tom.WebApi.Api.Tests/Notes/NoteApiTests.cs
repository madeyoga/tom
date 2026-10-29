using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Tom.WebApi.Api.Data;
using Tom.WebApi.Api.Notes;

namespace Tom.WebApi.Api.Tests.Notes;

[Collection("api")]
public sealed class NoteApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Anonymous_list_returns_401()
    {
        var response = await factory.AnonymousClient().GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_get_returns_401()
    {
        var response = await factory.AnonymousClient().GetAsync($"/api/notes/{NoteId.New()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_create_returns_401()
    {
        var response = await factory.AnonymousClient().PostAsJsonAsync("/api/notes", new { title = "Secret" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_archive_returns_401()
    {
        var response = await factory.AnonymousClient().PostAsync($"/api/notes/{NoteId.New()}/archive", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_without_permission_gets_403()
    {
        var client = await factory.UnprivilegedClientAsync();
        var response = await client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_notes_for_the_signed_in_user()
    {
        var client = await factory.AdminClientAsync();
        var response = await client.GetAsync("/api/notes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_returns_201_with_location()
    {
        var client = await factory.AdminClientAsync();
        var title = $"Note {Guid.CreateVersion7()}";
        var response = await client.PostAsJsonAsync("/api/notes", new { title });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using (var document = JsonDocument.Parse(json))
        {
            var idElement = document.RootElement.GetProperty("id");
            Assert.Equal(JsonValueKind.String, idElement.ValueKind);
            Assert.True(Guid.TryParse(idElement.GetString(), out var parsed));
            Assert.Equal(7, parsed.Version);
        }

        var created = JsonSerializer.Deserialize<NoteResponse>(json, JsonSerializerOptions.Web);
        Assert.NotNull(created);
        Assert.Equal(title, created.Title);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/notes/{created.Id}", response.Headers.Location!.ToString());

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var fetchedJson = await fetched.Content.ReadAsStringAsync();
        using (var fetchedDocument = JsonDocument.Parse(fetchedJson))
        {
            var idElement = fetchedDocument.RootElement.GetProperty("id");
            Assert.Equal(JsonValueKind.String, idElement.ValueKind);
            Assert.Equal(created.Id.Value.ToString(), idElement.GetString());
        }

        var body = JsonSerializer.Deserialize<NoteResponse>(fetchedJson, JsonSerializerOptions.Web);
        Assert.Equal(created.Id, body!.Id);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_validation_problem()
    {
        var client = await factory.AdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/notes", new { title = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Create_duplicate_title_returns_conflict()
    {
        var client = await factory.AdminClientAsync();
        var title = $"Dup {Guid.CreateVersion7()}";
        (await client.PostAsJsonAsync("/api/notes", new { title })).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/notes", new { title });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_missing_note_returns_404()
    {
        var client = await factory.AdminClientAsync();
        var response = await client.GetAsync($"/api/notes/{NoteId.New()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_missing_note_returns_404()
    {
        var client = await factory.AdminClientAsync();
        var response = await client.PostAsync($"/api/notes/{NoteId.New()}/archive", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Archive_then_again_returns_conflict()
    {
        var client = await factory.AdminClientAsync();
        var created = await client.PostAsJsonAsync(
            "/api/notes",
            new { title = $"Archive {Guid.CreateVersion7()}" });
        created.EnsureSuccessStatusCode();
        var note = await created.Content.ReadFromJsonAsync<NoteResponse>();

        var archived = await client.PostAsync($"/api/notes/{note!.Id}/archive", content: null);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        var again = await client.PostAsync($"/api/notes/{note.Id}/archive", content: null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Create_uses_the_injected_clock()
    {
        var instant = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var fake = new FakeTimeProvider(instant);
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(fake);
        }));

        var client = await ApiFactory.SignInAsync(app, ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        var response = await client.PostAsJsonAsync(
            "/api/notes",
            new { title = $"Clock {Guid.CreateVersion7()}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.Equal(instant, created!.CreatedAt);
    }

    [Fact]
    public async Task OpenApi_describes_note_id_as_a_uuid_string()
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("OpenApi:Enabled", "true");
            builder.UseSetting(
                "AuthEndpoints:EmailConfirmation:ConfirmEmailRedirectUri",
                "https://localhost/confirm-email");
            builder.UseSetting(
                "AuthEndpoints:EmailConfirmation:AllowedRedirectOrigins:0",
                "https://localhost");
        });

        var json = await app.CreateClient().GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);
        var id = NoteIdSchema(document.RootElement);
        Assert.Equal("string", id.GetProperty("type").GetString());
        Assert.Equal("uuid", id.GetProperty("format").GetString());
    }

    private static JsonElement NoteIdSchema(JsonElement document)
    {
        var schemas = document.GetProperty("components").GetProperty("schemas");
        var id = schemas.GetProperty("NoteResponse").GetProperty("properties").GetProperty("id");
        if (id.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/').Last();
            return schemas.GetProperty(name);
        }

        return id;
    }
}

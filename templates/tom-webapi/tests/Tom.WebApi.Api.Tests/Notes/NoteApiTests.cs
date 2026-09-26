using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
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
        var response = await factory.AnonymousClient().GetAsync("/api/notes/1");
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
        var response = await factory.AnonymousClient().PostAsync("/api/notes/1/archive", content: null);
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
        var created = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(created);
        Assert.Equal(title, created.Title);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/notes/{created.Id}", response.Headers.Location!.ToString());

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.Content.ReadFromJsonAsync<NoteResponse>();
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
        var response = await client.GetAsync("/api/notes/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_missing_note_returns_404()
    {
        var client = await factory.AdminClientAsync();
        var response = await client.PostAsync("/api/notes/999999/archive", content: null);
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
}

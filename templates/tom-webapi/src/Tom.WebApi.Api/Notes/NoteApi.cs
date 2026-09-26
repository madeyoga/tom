using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AuthEndpoints.Identity;
using Tom.WebApi.Api.Infrastructure.Data;
using Tom.WebApi.Api.Notes.Contracts;
using Tom.WebApi.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Api.Notes;

public static class NoteApi
{
    public static IEndpointConventionBuilder MapNoteApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/notes").WithTags("Notes");

        group.MapGet("/", ListNotes)
            .WithName("ListNotes")
            .WithSummary("List my notes")
            .RequireAuthorization(NotesPermissions.View);
        group.MapGet("/{id:long}", GetNote)
            .WithName("GetNote")
            .WithSummary("Get a note")
            .RequireAuthorization(NotesPermissions.View);
        group.MapPost("/", CreateNote)
            .WithName("CreateNote")
            .WithSummary("Create a note")
            .RequireAuthorization(NotesPermissions.Manage)
            .RequireAntiforgery()
            .EnableAntiforgery();
        group.MapPost("/{id:long}/archive", ArchiveNote)
            .WithName("ArchiveNote")
            .WithSummary("Archive a note")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(NotesPermissions.Manage)
            .RequireAntiforgery()
            .EnableAntiforgery();

        return group;
    }

    private static async Task<Ok<PaginatedItems<NoteListItem>>> ListNotes(
        AppDbContext db,
        CurrentUser user,
        [AsParameters] PaginationRequest page,
        [Description("Filter by title.")] string? search,
        CancellationToken cancellationToken)
    {
        var userId = user.RequiredUserId;
        var query = db.Notes().AsNoTracking().Where(note => note.OwnerUserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(note => EF.Functions.ILike(note.Title, term));
        }

        var result = await Pagination.CreateAsync(
            page.PageIndex,
            page.PageSize,
            query.OrderByDescending(note => note.CreatedAt)
                .Select(note => new NoteListItem(note.Id, note.Title, note.CreatedAt)),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<NoteResponse>, NotFound>> GetNote(
        long id,
        AppDbContext db,
        CurrentUser user,
        CancellationToken cancellationToken)
    {
        var note = await db.Notes().AsNoTracking()
            .Where(item => item.Id == id && item.OwnerUserId == user.RequiredUserId)
            .Select(item => new NoteResponse(item.Id, item.Title, item.Body, item.CreatedAt, item.ArchivedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return note is null ? TypedResults.NotFound() : TypedResults.Ok(note);
    }

    private static async Task<Results<CreatedAtRoute<NoteResponse>, ProblemHttpResult>> CreateNote(
        CreateNoteRequest request,
        AppDbContext db,
        CurrentUser user,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.RequiredUserId;
        var title = request.Title.Trim();
        if (await db.Notes().AnyAsync(
            note => note.OwnerUserId == userId && note.Title == title,
            cancellationToken))
        {
            return Problems.Conflict("Duplicate note", $"You already have a note titled '{title}'.");
        }

        var note = new Note
        {
            Title = title,
            Body = request.Body,
            OwnerUserId = userId,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Notes().Add(note);
        await db.SaveChangesAsync(cancellationToken);

        var body = new NoteResponse(note.Id, note.Title, note.Body, note.CreatedAt, null);
        return TypedResults.CreatedAtRoute(body, "GetNote", new { id = note.Id });
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ArchiveNote(
        long id,
        NotesCommands notes,
        CurrentUser user,
        CancellationToken cancellationToken)
    {
        var result = await notes.ArchiveAsync(id, user.RequiredUserId, cancellationToken);
        if (result.Problem is { } problem)
        {
            return problem;
        }

        return TypedResults.NoContent();
    }
}

public sealed record CreateNoteRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Title,
    [property: StringLength(4000)] string? Body);

public sealed record NoteResponse(
    long Id,
    string Title,
    string? Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt);

public sealed record NoteListItem(long Id, string Title, DateTimeOffset CreatedAt);

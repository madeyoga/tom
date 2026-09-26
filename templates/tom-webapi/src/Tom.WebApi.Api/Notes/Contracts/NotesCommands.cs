using Tom.WebApi.Api.Infrastructure.Data;
using Tom.WebApi.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Api.Notes.Contracts;

public sealed class NotesCommands(AppDbContext db, TimeProvider clock)
{
    public async Task<Result> ArchiveAsync(long noteId, Guid userId, CancellationToken cancellationToken)
    {
        var note = await db.Notes().FirstOrDefaultAsync(
            item => item.Id == noteId && item.OwnerUserId == userId,
            cancellationToken);
        if (note is null)
        {
            return Problems.NotFound("Note not found", $"Note {noteId} does not exist.");
        }

        if (note.ArchivedAt is not null)
        {
            return Problems.Conflict("Already archived", "This note is already archived.");
        }

        note.ArchivedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}

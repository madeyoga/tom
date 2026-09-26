using Tom.WebApi.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Api.Notes.Contracts;

public sealed class NotesQueries(AppDbContext db)
{
    public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken)
        => db.Notes().CountAsync(
            note => note.OwnerUserId == userId && note.ArchivedAt == null,
            cancellationToken);
}

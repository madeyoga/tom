using Tom.WebApi.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Api.Notes;

internal static class NotesDb
{
    public static DbSet<Note> Notes(this AppDbContext db) => db.Set<Note>();
}

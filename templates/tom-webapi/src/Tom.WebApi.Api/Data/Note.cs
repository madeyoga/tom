using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tom.WebApi.Api.Data;

internal sealed class Note
{
    public long Id { get; set; }

    public required string Title { get; set; }

    public string? Body { get; set; }

    public Guid OwnerUserId { get; set; }

    public AppUser Owner { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.Property(note => note.Id).UseIdentityAlwaysColumn();
        builder.Property(note => note.Title).HasMaxLength(200);
        builder.Property(note => note.Body).HasMaxLength(4000);
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        builder.HasIndex(note => new { note.OwnerUserId, note.CreatedAt });
        builder.HasOne(note => note.Owner)
            .WithMany(user => user.Notes)
            .HasForeignKey(note => note.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal static class NoteSet
{
    public static DbSet<Note> Notes(this AppDbContext db) => db.Set<Note>();
}

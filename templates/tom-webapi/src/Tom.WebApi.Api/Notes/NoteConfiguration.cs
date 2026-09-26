using Tom.WebApi.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tom.WebApi.Api.Notes;

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
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(note => note.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

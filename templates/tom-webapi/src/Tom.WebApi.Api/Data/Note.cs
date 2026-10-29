using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Tom.WebApi.Api.Data;

[JsonConverter(typeof(NoteIdJsonConverter))]
public readonly record struct NoteId(Guid Value) : IParsable<NoteId>
{
    public static NoteId New() => new(Guid.CreateVersion7());

    public static NoteId Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result)
            ? result
            : throw new FormatException($"'{s}' is not a NoteId.");

    public static bool TryParse(string? s, IFormatProvider? provider, out NoteId result)
    {
        if (Guid.TryParse(s, provider, out var value))
        {
            result = new NoteId(value);
            return true;
        }

        result = default;
        return false;
    }

    // Link generation and logs use the Guid text. The record format is not a route value.
    public override string ToString() => Value.ToString();
}

public sealed class NoteIdJsonConverter : JsonConverter<NoteId>
{
    public override NoteId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var value))
        {
            throw new JsonException("A NoteId must be a GUID string.");
        }

        return new NoteId(value);
    }

    public override void Write(Utf8JsonWriter writer, NoteId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

public sealed class NoteIdValueConverter : ValueConverter<NoteId, Guid>
{
    public NoteIdValueConverter()
        : base(id => id.Value, value => new NoteId(value))
    {
    }
}

public sealed class Note
{
    public NoteId Id { get; set; } = NoteId.New();

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
        builder.Property(note => note.Id).ValueGeneratedNever();
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

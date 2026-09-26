namespace Tom.WebApi.Api.Notes;

internal sealed class Note
{
    public long Id { get; set; }

    public required string Title { get; set; }

    public string? Body { get; set; }

    public Guid OwnerUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}

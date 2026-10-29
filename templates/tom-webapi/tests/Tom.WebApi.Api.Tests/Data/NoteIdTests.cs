using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Tom.WebApi.Api.Data;

namespace Tom.WebApi.Api.Tests.Data;

public sealed class NoteIdTests
{
    [Fact]
    public void New_id_is_a_version_7_guid()
    {
        var id = NoteId.New();
        Assert.Equal(7, id.Value.Version);
        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void Json_round_trips_as_a_guid_string()
    {
        var id = NoteId.New();
        var json = JsonSerializer.Serialize(id);
        Assert.Equal($"\"{id.Value:D}\"", json);

        var back = JsonSerializer.Deserialize<NoteId>(json);
        Assert.Equal(id, back);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, document.RootElement.ValueKind);
    }

    [Fact]
    public void Json_rejects_a_value_that_is_not_a_guid_string()
    {
        var guid = NoteId.New().Value.ToString("D");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NoteId>("\"not-a-guid\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NoteId>($$"""{"value":"{{guid}}"}"""));
    }

    [Fact]
    public void Value_converter_round_trips_the_guid()
    {
        var converter = new NoteIdValueConverter();
        var toProvider = converter.ConvertToProviderExpression.Compile();
        var fromProvider = converter.ConvertFromProviderExpression.Compile();
        var id = NoteId.New();

        var stored = toProvider(id);
        Assert.Equal(id.Value, stored);
        Assert.Equal(id, fromProvider(stored));
    }

    [Fact]
    public void Parse_round_trips_the_guid_text()
    {
        var id = NoteId.New();
        Assert.Equal(id, NoteId.Parse(id.ToString(), CultureInfo.InvariantCulture));
        Assert.True(NoteId.TryParse(id.Value.ToString("D"), CultureInfo.InvariantCulture, out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(NoteId.TryParse("not-a-guid", CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void Note_id_column_is_a_uuid_generated_by_the_app()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=typed_ids;Username=postgres;Password=postgres")
            .Options;
        using var db = new AppDbContext(options);
        var property = db.Model.FindEntityType(typeof(Note))!.FindProperty(nameof(Note.Id))!;

        Assert.Equal(typeof(NoteId), property.ClrType);
        var converter = property.GetValueConverter();
        Assert.NotNull(converter);
        Assert.Equal(typeof(Guid), converter.ProviderClrType);
        Assert.Equal(typeof(NoteId), converter.ModelClrType);
        Assert.Contains("uuid", property.GetColumnType(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);

        var id = NoteId.New();
        Assert.Equal(id.Value, converter.ConvertToProvider(id));
        Assert.Equal(id, converter.ConvertFromProvider(id.Value));
    }
}

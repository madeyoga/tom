namespace Tom.WebApi.Api.Shared;

public interface IDataSeeder
{
    int Order { get; }

    bool IsCritical { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}

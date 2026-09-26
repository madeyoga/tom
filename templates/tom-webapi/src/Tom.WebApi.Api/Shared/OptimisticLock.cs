using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Api.Shared;

public static class OptimisticLock
{
    public static async Task ExecuteAsync(
        DbContext dbContext,
        Func<Task> action,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();

                if (attempt >= maxAttempts)
                {
                    throw;
                }
            }
        }
    }
}

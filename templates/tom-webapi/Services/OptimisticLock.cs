using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Services;

public static class OptimisticLock
{
    public static async Task ExecuteAsync(DbContext dbContext, Func<Task> action, int maxAttempts = 3)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                await action();
                await transaction.CommitAsync();
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                dbContext.ChangeTracker.Clear();

                if (attempt >= maxAttempts)
                {
                    throw;
                }
            }
        }
    }
}

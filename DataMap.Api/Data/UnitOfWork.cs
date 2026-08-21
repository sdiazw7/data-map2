using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Data;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<Task> operation)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        });
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        // Join an outer transaction rather than opening a second one, which Npgsql rejects.
        // ProjectionRepository.RefreshAsync opens its own when called on its own, so any
        // service that calls it inside a wider unit of work lands here.
        if (db.Database.CurrentTransaction is not null)
            return await operation();

        // The execution strategy owns the retry loop. Opening the transaction inside it means
        // a retried attempt starts a fresh transaction instead of resuming a rolled-back one.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await operation();
            await transaction.CommitAsync();
            return result;
        });
    }
}

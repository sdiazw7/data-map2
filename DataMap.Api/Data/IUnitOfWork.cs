namespace DataMap.Api.Data;

/// <summary>
/// Runs a group of repository calls inside a single database transaction.
/// <para>
/// Every repository commits its own <c>SaveChangesAsync</c>, so without this a multi-step
/// service operation commits piecemeal: a failure partway through leaves the earlier writes
/// applied and the later ones — including audit records and the projection sync — missing.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<Task> operation);

    Task<T> ExecuteAsync<T>(Func<Task<T>> operation);
}

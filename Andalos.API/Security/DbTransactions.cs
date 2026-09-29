using Andalos.API.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace Andalos.API.Security;
public static class DbTransactions
{
    public static async Task<T> AtomicAsync<T>(this AppDbContext db, Func<Task<T>> operation)
    {
        if (db.Database.CurrentTransaction is not null) return await operation();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try { var result = await operation(); await tx.CommitAsync(); return result; }
        catch { await tx.RollbackAsync(); throw; }
    }
    public static async Task AtomicAsync(this AppDbContext db, Func<Task> operation) =>
        await db.AtomicAsync(async () => { await operation(); return true; });
}

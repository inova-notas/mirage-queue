using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace MirageQueue.Retry;

public static class TransientClassifier
{
    /// <summary>
    /// Default transient predicate. Recognizes BCL/EF transient exceptions:
    /// TimeoutException, DbUpdateConcurrencyException, SocketException, and
    /// Npgsql.PostgresException with transient SQL state codes (recognised via
    /// type-name + reflection so the core package doesn't take a hard Npgsql
    /// reference). Cancellation is NOT considered transient (caller-initiated).
    /// </summary>
    public static readonly Func<Exception, bool> Default = IsDefaultTransient;

    // Resolving Npgsql.PostgresException's SqlState property via reflection happens
    // on the failure path; cache the PropertyInfo lookup per concrete type so we
    // pay it once. Using a concurrent dictionary keyed by Type — the type name
    // check still gates entry, so this only ever caches Postgres exception types.
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> SqlStatePropertyCache = new();

    private static bool IsDefaultTransient(Exception ex)
    {
        switch (ex)
        {
            case OperationCanceledException:
                return false;
            case TimeoutException:
            case DbUpdateConcurrencyException:
            case SocketException:
                return true;
        }

        if (IsTransientPostgresException(ex))
            return true;

        return ex.InnerException is { } inner && IsDefaultTransient(inner);
    }

    private static bool IsTransientPostgresException(Exception ex)
    {
        var type = ex.GetType();
        if (type.FullName != "Npgsql.PostgresException")
            return false;

        var property = SqlStatePropertyCache.GetOrAdd(type, static t => t.GetProperty("SqlState"));
        var sqlState = property?.GetValue(ex) as string;
        return sqlState is not null && IsTransientSqlState(sqlState);
    }

    // Conservative list of transient Postgres SQL state codes.
    // 40001 serialization_failure, 40P01 deadlock_detected,
    // 08* connection exceptions, 57P03 cannot_connect_now, 53300 too_many_connections.
    private static bool IsTransientSqlState(string sqlState) =>
        sqlState is "40001" or "40P01" or "57P03" or "53300"
        || sqlState.StartsWith("08", StringComparison.Ordinal);
}

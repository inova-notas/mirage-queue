using MirageQueue.Messages.Entities;
using Npgsql;

namespace MirageQueue.IntegrationTests.Fixtures;

internal static class PostgresFixtureSeedExtensions
{
    /// <summary>
    /// Insert a minimal inbound message row directly (bypassing the publisher API)
    /// so foreign-key references from outbound rows are satisfied without exercising
    /// publisher behavior. <paramref name="status"/> and <paramref name="updateAt"/>
    /// expose the fields that matter for status- or time-based test predicates.
    /// </summary>
    public static async Task<Guid> SeedInboundAsync(
        this PostgresFixture fixture,
        InboundMessageStatus status = InboundMessageStatus.Queued,
        DateTime? updateAt = null)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mirage_queue."InboundMessage"
                ("Id", "Status", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES (@id, @status, '{}'::jsonb, 'X', now(), @updateAt)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)status));
        cmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)updateAt ?? DBNull.Value));
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Insert an outbound row directly with full control over status, attempt count,
    /// retry timestamps, and <c>UpdateAt</c>. Used to set up scenarios that the
    /// regular fan-out path wouldn't produce.
    /// </summary>
    public static async Task<Guid> SeedOutboundAsync(
        this PostgresFixture fixture,
        Guid inboundId,
        string endpoint,
        OutboundMessageStatus status,
        DateTime? nextRetryAt = null,
        DateTime? processingStartedAt = null,
        DateTime? updateAt = null,
        int attemptCount = 0)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mirage_queue."OutboundMessage"
                ("Id", "Status", "ConsumerEndpoint", "InboundMessageId",
                 "Content", "MessageContract", "CreateAt", "UpdateAt",
                 "AttemptCount", "NextRetryAt", "ProcessingStartedAt")
            VALUES (@id, @status, @endpoint, @inboundId,
                    '{}'::jsonb, 'X', now(), @updateAt,
                    @attempts, @nextRetry, @processingStarted)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)status));
        cmd.Parameters.Add(new NpgsqlParameter("endpoint", endpoint));
        cmd.Parameters.Add(new NpgsqlParameter("inboundId", inboundId));
        cmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)updateAt ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("attempts", attemptCount));
        cmd.Parameters.Add(new NpgsqlParameter("nextRetry", (object?)nextRetryAt ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("processingStarted", (object?)processingStartedAt ?? DBNull.Value));
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Insert a scheduled inbound row directly.
    /// </summary>
    public static async Task<Guid> SeedScheduledAsync(
        this PostgresFixture fixture,
        ScheduledInboundMessageStatus status,
        DateTime? updateAt = null,
        DateTime? executeAt = null)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mirage_queue."ScheduledInboundMessage"
                ("Id", "Status", "ExecuteAt", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES (@id, @status, @executeAt, '{}'::jsonb, 'X', now(), @updateAt)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)status));
        cmd.Parameters.Add(new NpgsqlParameter("executeAt", (object?)executeAt ?? DateTime.UtcNow));
        cmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)updateAt ?? DBNull.Value));
        await cmd.ExecuteNonQueryAsync();
        return id;
    }
}

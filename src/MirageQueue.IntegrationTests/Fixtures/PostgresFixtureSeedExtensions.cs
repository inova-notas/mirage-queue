using MirageQueue.Messages.Entities;
using Npgsql;

namespace MirageQueue.IntegrationTests.Fixtures;

internal static class PostgresFixtureSeedExtensions
{
    /// <summary>
    /// Insert a minimal inbound message row directly (bypassing the publisher API)
    /// so foreign-key references from outbound rows are satisfied without exercising
    /// publisher behavior.
    /// </summary>
    public static async Task<Guid> SeedInboundAsync(this PostgresFixture fixture)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO mirage_queue."InboundMessage"
                ("Id", "Status", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES (@id, 1, '{}'::jsonb, 'X', now(), now())
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", id));
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    /// <summary>
    /// Insert an outbound row directly with full control over status, attempt count,
    /// next-retry-at, and processing-started-at. Used to set up scenarios that the
    /// regular fan-out path wouldn't produce.
    /// </summary>
    public static async Task<Guid> SeedOutboundAsync(
        this PostgresFixture fixture,
        Guid inboundId,
        string endpoint,
        OutboundMessageStatus status,
        DateTime? nextRetryAt = null,
        DateTime? processingStartedAt = null,
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
                    '{}'::jsonb, 'X', now(), now(),
                    @attempts, @nextRetry, @processingStarted)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)status));
        cmd.Parameters.Add(new NpgsqlParameter("endpoint", endpoint));
        cmd.Parameters.Add(new NpgsqlParameter("inboundId", inboundId));
        cmd.Parameters.Add(new NpgsqlParameter("attempts", attemptCount));
        cmd.Parameters.Add(new NpgsqlParameter("nextRetry", (object?)nextRetryAt ?? DBNull.Value));
        cmd.Parameters.Add(new NpgsqlParameter("processingStarted", (object?)processingStartedAt ?? DBNull.Value));
        await cmd.ExecuteNonQueryAsync();
        return id;
    }
}

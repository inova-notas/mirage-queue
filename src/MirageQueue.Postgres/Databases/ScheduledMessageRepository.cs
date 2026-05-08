using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class ScheduledMessageRepository : BaseRepository<MirageQueueDbContext, ScheduledInboundMessage>, IScheduledMessageRepository
{
    readonly MirageQueueDbContext _dbContext;
    readonly NpgsqlParameter _statusParam;

    public ScheduledMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        _statusParam = new NpgsqlParameter("statusParam", DbType.Int32)
        {
            Value = (int)ScheduledInboundMessageStatus.WaitingScheduledTime
        };
    }

    public async Task<List<ScheduledInboundMessage>> GetScheduledMessages(IDbContextTransaction? transaction = null, int limit = 10)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
        
        var nowParam = new NpgsqlParameter("nowParam", DateTime.UtcNow);

        return await _dbContext.Set<ScheduledInboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"ScheduledInboundMessage\" WHERE \"Status\" = {_statusParam} AND \"ExecuteAt\" <= {nowParam} FOR UPDATE SKIP LOCKED LIMIT {limit}")
            .AsNoTracking()
            .ToListAsync();
    }
    
    public async Task UpdateMessageStatus(Guid id, ScheduledInboundMessageStatus status, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"ScheduledInboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam} WHERE \"Id\" = {idParam}");
    }

    public async Task InsertDirect(ScheduledInboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Connection is null)
            throw new InvalidOperationException("The supplied transaction is not associated with a connection.");

        await using var cmd = transaction.Connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO mirage_queue."ScheduledInboundMessage"
                ("Id", "Status", "ExecuteAt", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES
                (@id, @status, @executeAt, @content::jsonb, @contract, @createAt, @updateAt)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", message.Id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)message.Status));
        cmd.Parameters.Add(new NpgsqlParameter("executeAt", message.ExecuteAt));
        cmd.Parameters.Add(new NpgsqlParameter("content", message.Content));
        cmd.Parameters.Add(new NpgsqlParameter("contract", message.MessageContract));
        cmd.Parameters.Add(new NpgsqlParameter("createAt", message.CreateAt));
        cmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)message.UpdateAt ?? DBNull.Value));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
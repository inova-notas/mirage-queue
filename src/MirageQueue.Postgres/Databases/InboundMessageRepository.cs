using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MirageQueue.Common;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;
using Npgsql;

namespace MirageQueue.Postgres.Databases;

public class InboundMessageRepository : BaseRepository<MirageQueueDbContext, InboundMessage>, IInboundMessageRepository
{
    readonly MirageQueueDbContext _dbContext;
    readonly NpgsqlParameter _statusParam;
    
    public InboundMessageRepository(MirageQueueDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
        _statusParam = new NpgsqlParameter("statusParam", DbType.Int32)
        {
            Value = (int)InboundMessageStatus.New
        };
    }

    public async Task<List<InboundMessage>> GetQueuedMessages(IDbContextTransaction? transaction = null, int limit = 10)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());
        
        return await _dbContext.Set<InboundMessage>()
            .FromSql($"SELECT * FROM mirage_queue.\"InboundMessage\" WHERE \"Status\" = {_statusParam} FOR UPDATE SKIP LOCKED LIMIT {limit}")
            .AsNoTracking()
            .ToListAsync();
    }
    
    public async Task UpdateMessageStatus(Guid id, InboundMessageStatus status, IDbContextTransaction? transaction = default)
    {
        if (transaction is not null)
            await _dbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        var idParam = new NpgsqlParameter("idParam", id);
        var statusUpdateParam = new NpgsqlParameter("statusUpdateParam", (int)status);
        var updatedParam = new NpgsqlParameter("updatedParam", DateTime.UtcNow);

        await _dbContext.Database.ExecuteSqlAsync($"UPDATE mirage_queue.\"InboundMessage\" SET \"Status\" = {statusUpdateParam}, \"UpdateAt\" = {updatedParam} WHERE \"Id\" = {idParam}");
    }

    public async Task InsertDirect(InboundMessage message, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.Connection is null)
            throw new InvalidOperationException("The supplied transaction is not associated with a connection.");

        await using var cmd = transaction.Connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO mirage_queue."InboundMessage"
                ("Id", "Status", "Content", "MessageContract", "CreateAt", "UpdateAt")
            VALUES
                (@id, @status, @content::jsonb, @contract, @createAt, @updateAt)
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", message.Id));
        cmd.Parameters.Add(new NpgsqlParameter("status", (int)message.Status));
        cmd.Parameters.Add(new NpgsqlParameter("content", message.Content));
        cmd.Parameters.Add(new NpgsqlParameter("contract", message.MessageContract));
        cmd.Parameters.Add(new NpgsqlParameter("createAt", message.CreateAt));
        cmd.Parameters.Add(new NpgsqlParameter("updateAt", (object?)message.UpdateAt ?? DBNull.Value));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
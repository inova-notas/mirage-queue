using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Workers;

public abstract class OutboundMessageHandlerWorker : BackgroundService, IMessageHandlerWorker
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboundMessageHandlerWorker> _logger;
    private readonly MirageQueueConfiguration _configuration;

    public OutboundMessageHandlerWorker(IServiceProvider serviceProvider,
        ILogger<OutboundMessageHandlerWorker> logger,
        MirageQueueConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbound message handler worker is running");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();

            var dbContext = GetContext(scope);
            var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
            await messageHandler.HandleQueuedOutboundMessages(transaction);
            
            await dbContext.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);
            
            await Task.Delay(TimeSpan.FromSeconds(_configuration.PoolingTime), stoppingToken);
        }
    }
    
    public abstract DbContext GetContext(AsyncServiceScope scope);
}
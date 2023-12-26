using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Workers;

public class OutboundMessageHandlerWorker : BackgroundService
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
            await messageHandler.HandleQueuedOutboundMessages();
            
            await Task.Delay(TimeSpan.FromSeconds(_configuration.PoolingTime), stoppingToken);
        }
    }
}
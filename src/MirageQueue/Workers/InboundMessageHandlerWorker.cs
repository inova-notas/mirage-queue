using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Workers;

public class InboundMessageHandlerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboundMessageHandlerWorker> _logger;
    private readonly MirageQueueConfiguration _configuration;

    public InboundMessageHandlerWorker(IServiceProvider serviceProvider,
        ILogger<InboundMessageHandlerWorker> logger,
        MirageQueueConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inbound message handler worker is running");
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var messageHandler = scope.ServiceProvider.GetRequiredService<IMessageHandler>();
            await messageHandler.HandleQueuedInboundMessages();
            
            await Task.Delay(TimeSpan.FromSeconds(_configuration.PoolingTime), stoppingToken);
        }
    }
}
using Microsoft.Extensions.Logging;
using MirageQueue.Workers;

namespace MirageQueue.Postgres.Workers;

public class PgMessageCleanupWorker(
    IServiceProvider serviceProvider,
    ILogger<MessageCleanupWorker> logger,
    MirageQueueConfiguration configuration)
    : MessageCleanupWorker(serviceProvider, logger, configuration);

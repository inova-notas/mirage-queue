using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MirageQueue.Messages.Repositories;
using MirageQueue.Workers;
using Moq;

namespace MirageQueue.Tests.Workers;

public class StuckProcessingRecoveryWorkerTests
{
    private class TestRecoveryWorker(
        IServiceProvider serviceProvider,
        ILogger<StuckProcessingRecoveryWorker> logger,
        MirageQueueConfiguration configuration,
        DbContext dbContext)
        : StuckProcessingRecoveryWorker(serviceProvider, logger, configuration)
    {
        public override DbContext GetContext(AsyncServiceScope scope) => dbContext;
    }

    [Fact]
    public async Task ExecuteAsync_WhenStuckMessagesExist_ShouldResetAndCommitTransaction()
    {
        var outboundRepo = new Mock<IOutboundMessageRepository>();
        var logger = new Mock<ILogger<StuckProcessingRecoveryWorker>>();
        var transaction = new Mock<IDbContextTransaction>();
        var databaseFacade = new Mock<DatabaseFacade>(new Mock<DbContext>().Object);
        var dbContext = new Mock<DbContext>();

        var configuration = new MirageQueueConfiguration
        {
            ProcessingRecoveryTimeInMinutes = 5,
            PoolingRecoveryTime = 100,
        };

        outboundRepo
            .Setup(x => x.ResetStuckProcessingMessages(5, It.IsAny<IDbContextTransaction>()))
            .ReturnsAsync(3);

        databaseFacade
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        dbContext.Setup(x => x.Database).Returns(databaseFacade.Object);

        var services = new ServiceCollection();
        services.AddScoped(_ => outboundRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new TestRecoveryWorker(serviceProvider, logger.Object, configuration, dbContext.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        outboundRepo.Verify(x => x.ResetStuckProcessingMessages(5, It.IsAny<IDbContextTransaction>()), Times.AtLeastOnce);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStuckMessages_ShouldNotLogWarning()
    {
        var outboundRepo = new Mock<IOutboundMessageRepository>();
        var logger = new Mock<ILogger<StuckProcessingRecoveryWorker>>();
        var transaction = new Mock<IDbContextTransaction>();
        var databaseFacade = new Mock<DatabaseFacade>(new Mock<DbContext>().Object);
        var dbContext = new Mock<DbContext>();

        var configuration = new MirageQueueConfiguration
        {
            ProcessingRecoveryTimeInMinutes = 5,
            PoolingRecoveryTime = 100,
        };

        outboundRepo
            .Setup(x => x.ResetStuckProcessingMessages(5, It.IsAny<IDbContextTransaction>()))
            .ReturnsAsync(0);

        databaseFacade
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        dbContext.Setup(x => x.Database).Returns(databaseFacade.Object);

        var services = new ServiceCollection();
        services.AddScoped(_ => outboundRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new TestRecoveryWorker(serviceProvider, logger.Object, configuration, dbContext.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        outboundRepo.Verify(x => x.ResetStuckProcessingMessages(5, It.IsAny<IDbContextTransaction>()), Times.AtLeastOnce);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenExceptionOccurs_ShouldRollbackAndLogError()
    {
        var outboundRepo = new Mock<IOutboundMessageRepository>();
        var logger = new Mock<ILogger<StuckProcessingRecoveryWorker>>();
        var transaction = new Mock<IDbContextTransaction>();
        var databaseFacade = new Mock<DatabaseFacade>(new Mock<DbContext>().Object);
        var dbContext = new Mock<DbContext>();

        var configuration = new MirageQueueConfiguration
        {
            ProcessingRecoveryTimeInMinutes = 5,
            PoolingRecoveryTime = 100,
        };

        outboundRepo
            .Setup(x => x.ResetStuckProcessingMessages(5, It.IsAny<IDbContextTransaction>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        databaseFacade
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        dbContext.Setup(x => x.Database).Returns(databaseFacade.Object);

        var services = new ServiceCollection();
        services.AddScoped(_ => outboundRepo.Object);
        var serviceProvider = services.BuildServiceProvider();

        var worker = new TestRecoveryWorker(serviceProvider, logger.Object, configuration, dbContext.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

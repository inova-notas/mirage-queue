using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Outbox;

namespace MirageQueue.Tests.Outbox;

public class MirageQueueOutboxExtensionsTests
{
    private class TestDbContext : DbContext
    {
    }

    [Fact]
    public void AddMirageQueueOutbox_RegistersWithScopedLifetime()
    {
        var services = new ServiceCollection();

        services.AddMirageQueueOutbox<TestDbContext>();

        var descriptor = Assert.Single(services, s => s.ServiceType == typeof(IDbContextOutbox<TestDbContext>));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(DbContextOutbox<TestDbContext>), descriptor.ImplementationType);
    }
}

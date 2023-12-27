using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MirageQueue.Workers;

public interface IMessageHandlerWorker
{
    DbContext GetContext(AsyncServiceScope scope);
}
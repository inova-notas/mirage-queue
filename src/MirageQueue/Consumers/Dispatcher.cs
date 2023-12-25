using MirageQueue.Consumers.Abstractions;

namespace MirageQueue.Consumers;

internal class Dispatcher
{
        
    private List<DispatcherConsumer>? Consumers { get; set; }

    internal void AddDispatchConsumer<TConsumer, TMessage>()  
    where TMessage : class
    where TConsumer : Consumer<TMessage>
    {
        
    }
    
    internal class DispatcherConsumer
    {
        public required string MessageContract { get; set; }
        public required string ConsumerEndpoint { get; set; }
        public required Type Consumer { get; set; }
    }
}
namespace MirageQueue.Dashboard.Models;

public class DashboardStatsViewModel
{
    public int TotalInboundMessages { get; set; }
    public int NewInboundMessages { get; set; }
    public int QueuedInboundMessages { get; set; }
    
    public int TotalOutboundMessages { get; set; }
    public int CreatingOutboundMessages { get; set; }
    public int NewOutboundMessages { get; set; }
    public int ProcessingOutboundMessages { get; set; }
    public int ProcessedOutboundMessages { get; set; }
    public int FailedOutboundMessages { get; set; }
    
    public int TotalScheduledMessages { get; set; }
    public int WaitingScheduledMessages { get; set; }
    public int QueuedScheduledMessages { get; set; }
}

public class MessageSummaryViewModel
{
    public Guid Id { get; set; }
    public string MessageContract { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreateAt { get; set; }
    public DateTime? UpdateAt { get; set; }
    public string MessageType { get; set; } = string.Empty; // "Inbound", "Outbound", "Scheduled"
    public string? ConsumerEndpoint { get; set; } // For OutboundMessage
    public DateTime? ExecuteAt { get; set; } // For ScheduledMessage
}

public class MessagesListViewModel
{
    public List<MessageSummaryViewModel> Messages { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalMessages { get; set; }
    public int PageSize { get; set; }
    public string? StatusFilter { get; set; }
    public string? ContractFilter { get; set; }
    public string? EndpointFilter { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public List<string> AvailableStatuses { get; set; } = new();
    public List<string> AvailableContracts { get; set; } = new();
    public List<string> AvailableEndpoints { get; set; } = new();
}

public class MessageDetailViewModel
{
    public Guid Id { get; set; }
    public string MessageContract { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreateAt { get; set; }
    public DateTime? UpdateAt { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string? ConsumerEndpoint { get; set; }
    public DateTime? ExecuteAt { get; set; }
    public Guid? InboundMessageId { get; set; }
    public bool CanRequeue { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? ExceptionType { get; set; }
}
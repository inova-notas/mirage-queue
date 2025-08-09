using MirageQueue.Dashboard.Models;

namespace MirageQueue.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardStatsViewModel> GetDashboardStatsAsync();
    Task<MessagesListViewModel> GetInboundMessagesAsync(int page = 1, int pageSize = 20, string? statusFilter = null);
    Task<MessagesListViewModel> GetOutboundMessagesAsync(int page = 1, int pageSize = 20, string? statusFilter = null, string? contractFilter = null, string? endpointFilter = null);
    Task<MessagesListViewModel> GetScheduledMessagesAsync(int page = 1, int pageSize = 20, string? statusFilter = null);
    Task<MessageDetailViewModel?> GetInboundMessageDetailAsync(Guid id);
    Task<MessageDetailViewModel?> GetOutboundMessageDetailAsync(Guid id);
    Task<MessageDetailViewModel?> GetScheduledMessageDetailAsync(Guid id);
    Task<bool> RequeueInboundMessageAsync(Guid id);
    Task<bool> RequeueOutboundMessageAsync(Guid id);
    Task<bool> RequeueScheduledMessageAsync(Guid id);
    Task<List<string>> GetDistinctContractsAsync();
    Task<List<string>> GetDistinctEndpointsAsync();
}
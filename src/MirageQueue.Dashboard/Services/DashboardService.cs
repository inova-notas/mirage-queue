using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MirageQueue.Dashboard.Models;
using MirageQueue.Messages.Entities;
using MirageQueue.Messages.Repositories;

namespace MirageQueue.Dashboard.Services;

public class DashboardService : IDashboardService
{
    private readonly IInboundMessageRepository _inboundRepository;
    private readonly IOutboundMessageRepository _outboundRepository;
    private readonly IScheduledMessageRepository _scheduledRepository;
    private readonly IMemoryCache _cache;

    public DashboardService(
        IInboundMessageRepository inboundRepository,
        IOutboundMessageRepository outboundRepository,
        IScheduledMessageRepository scheduledRepository,
        IMemoryCache cache)
    {
        _inboundRepository = inboundRepository;
        _outboundRepository = outboundRepository;
        _scheduledRepository = scheduledRepository;
        _cache = cache;
    }

    public async Task<DashboardStatsViewModel> GetDashboardStatsAsync()
    {
        var inboundMessages = await _inboundRepository.GetAllNoTrackingAsync();
        var outboundMessages = await _outboundRepository.GetAllNoTrackingAsync();
        var scheduledMessages = await _scheduledRepository.GetAllNoTrackingAsync();

        var inboundList = await inboundMessages.ToListAsync();
        var outboundList = await outboundMessages.ToListAsync();
        var scheduledList = await scheduledMessages.ToListAsync();

        return new DashboardStatsViewModel
        {
            TotalInboundMessages = inboundList.Count,
            NewInboundMessages = inboundList.Count(m => m.Status == InboundMessageStatus.New),
            QueuedInboundMessages = inboundList.Count(m => m.Status == InboundMessageStatus.Queued),

            TotalOutboundMessages = outboundList.Count,
            CreatingOutboundMessages = outboundList.Count(m => m.Status == OutboundMessageStatus.Creating),
            NewOutboundMessages = outboundList.Count(m => m.Status == OutboundMessageStatus.New),
            ProcessingOutboundMessages = outboundList.Count(m => m.Status == OutboundMessageStatus.Processing),
            ProcessedOutboundMessages = outboundList.Count(m => m.Status == OutboundMessageStatus.Processed),
            FailedOutboundMessages = outboundList.Count(m => m.Status == OutboundMessageStatus.Failed),

            TotalScheduledMessages = scheduledList.Count,
            WaitingScheduledMessages = scheduledList.Count(m => m.Status == ScheduledInboundMessageStatus.WaitingScheduledTime),
            QueuedScheduledMessages = scheduledList.Count(m => m.Status == ScheduledInboundMessageStatus.Queued)
        };
    }

    public async Task<MessagesListViewModel> GetInboundMessagesAsync(int page = 1, int pageSize = 20, string? statusFilter = null)
    {
        var query = await _inboundRepository.GetAllNoTrackingAsync();
        
        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<InboundMessageStatus>(statusFilter, out var status))
        {
            query = query.Where(m => m.Status == status);
        }

        var totalMessages = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalMessages / pageSize);

        var messages = await query
            .OrderByDescending(m => m.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageSummaryViewModel
            {
                Id = m.Id,
                MessageContract = m.MessageContract,
                Content = m.Content,
                Status = m.Status.ToString(),
                CreateAt = m.CreateAt,
                UpdateAt = m.UpdateAt,
                MessageType = "Inbound"
            })
            .ToListAsync();

        return new MessagesListViewModel
        {
            Messages = messages,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalMessages = totalMessages,
            PageSize = pageSize,
            StatusFilter = statusFilter,
            MessageType = "Inbound",
            AvailableStatuses = Enum.GetNames<InboundMessageStatus>().ToList()
        };
    }

    public async Task<MessagesListViewModel> GetOutboundMessagesAsync(int page = 1, int pageSize = 20, string? statusFilter = null, string? contractFilter = null, string? endpointFilter = null)
    {
        var query = await _outboundRepository.GetAllNoTrackingAsync();
        
        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<OutboundMessageStatus>(statusFilter, out var status))
        {
            query = query.Where(m => m.Status == status);
        }
        
        if (!string.IsNullOrEmpty(contractFilter))
        {
            query = query.Where(m => m.MessageContract == contractFilter);
        }
        
        if (!string.IsNullOrEmpty(endpointFilter))
        {
            query = query.Where(m => m.ConsumerEndpoint == endpointFilter);
        }

        var totalMessages = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalMessages / pageSize);

        var messages = await query
            .OrderByDescending(m => m.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageSummaryViewModel
            {
                Id = m.Id,
                MessageContract = m.MessageContract,
                Content = m.Content,
                Status = m.Status.ToString(),
                CreateAt = m.CreateAt,
                UpdateAt = m.UpdateAt,
                MessageType = "Outbound",
                ConsumerEndpoint = m.ConsumerEndpoint
            })
            .ToListAsync();

        // Get distinct values for dropdowns
        var availableContracts = await GetDistinctContractsAsync();
        var availableEndpoints = await GetDistinctEndpointsAsync();

        return new MessagesListViewModel
        {
            Messages = messages,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalMessages = totalMessages,
            PageSize = pageSize,
            StatusFilter = statusFilter,
            ContractFilter = contractFilter,
            EndpointFilter = endpointFilter,
            MessageType = "Outbound",
            AvailableStatuses = Enum.GetNames<OutboundMessageStatus>().ToList(),
            AvailableContracts = availableContracts,
            AvailableEndpoints = availableEndpoints
        };
    }

    public async Task<MessagesListViewModel> GetScheduledMessagesAsync(int page = 1, int pageSize = 20, string? statusFilter = null)
    {
        var query = await _scheduledRepository.GetAllNoTrackingAsync();
        
        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<ScheduledInboundMessageStatus>(statusFilter, out var status))
        {
            query = query.Where(m => m.Status == status);
        }

        var totalMessages = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalMessages / pageSize);

        var messages = await query
            .OrderByDescending(m => m.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageSummaryViewModel
            {
                Id = m.Id,
                MessageContract = m.MessageContract,
                Content = m.Content,
                Status = m.Status.ToString(),
                CreateAt = m.CreateAt,
                UpdateAt = m.UpdateAt,
                MessageType = "Scheduled",
                ExecuteAt = m.ExecuteAt
            })
            .ToListAsync();

        return new MessagesListViewModel
        {
            Messages = messages,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalMessages = totalMessages,
            PageSize = pageSize,
            StatusFilter = statusFilter,
            MessageType = "Scheduled",
            AvailableStatuses = Enum.GetNames<ScheduledInboundMessageStatus>().ToList()
        };
    }

    public async Task<MessageDetailViewModel?> GetInboundMessageDetailAsync(Guid id)
    {
        var message = await _inboundRepository.FirstOrDefaultAsync(m => m.Id == id);
        if (message == null) return null;

        return new MessageDetailViewModel
        {
            Id = message.Id,
            MessageContract = message.MessageContract,
            Content = message.Content,
            Status = message.Status.ToString(),
            CreateAt = message.CreateAt,
            UpdateAt = message.UpdateAt,
            MessageType = "Inbound",
            CanRequeue = message.Status == InboundMessageStatus.Queued
        };
    }

    public async Task<MessageDetailViewModel?> GetOutboundMessageDetailAsync(Guid id)
    {
        var message = await _outboundRepository.FirstOrDefaultAsync(m => m.Id == id);
        if (message == null) return null;

        return new MessageDetailViewModel
        {
            Id = message.Id,
            MessageContract = message.MessageContract,
            Content = message.Content,
            Status = message.Status.ToString(),
            CreateAt = message.CreateAt,
            UpdateAt = message.UpdateAt,
            MessageType = "Outbound",
            ConsumerEndpoint = message.ConsumerEndpoint,
            InboundMessageId = message.InboundMessageId,
            CanRequeue = message.Status == OutboundMessageStatus.Failed || message.Status == OutboundMessageStatus.Processed,
            ErrorMessage = message.ErrorMessage,
            StackTrace = message.StackTrace,
            ExceptionType = message.ExceptionType
        };
    }

    public async Task<MessageDetailViewModel?> GetScheduledMessageDetailAsync(Guid id)
    {
        var message = await _scheduledRepository.FirstOrDefaultAsync(m => m.Id == id);
        if (message == null) return null;

        return new MessageDetailViewModel
        {
            Id = message.Id,
            MessageContract = message.MessageContract,
            Content = message.Content,
            Status = message.Status.ToString(),
            CreateAt = message.CreateAt,
            UpdateAt = message.UpdateAt,
            MessageType = "Scheduled",
            ExecuteAt = message.ExecuteAt,
            CanRequeue = message.Status == ScheduledInboundMessageStatus.Queued
        };
    }

    public async Task<bool> RequeueInboundMessageAsync(Guid id)
    {
        try
        {
            await _inboundRepository.UpdateMessageStatus(id, InboundMessageStatus.New);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RequeueOutboundMessageAsync(Guid id)
    {
        try
        {
            await _outboundRepository.UpdateMessageStatus(id, OutboundMessageStatus.New);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RequeueScheduledMessageAsync(Guid id)
    {
        try
        {
            await _scheduledRepository.UpdateMessageStatus(id, ScheduledInboundMessageStatus.WaitingScheduledTime);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetDistinctContractsAsync()
    {
        const string cacheKey = "mirage-distinct-contracts";
        
        if (_cache.TryGetValue(cacheKey, out List<string>? cachedContracts) && cachedContracts != null)
        {
            return cachedContracts;
        }

        var query = await _outboundRepository.GetAllNoTrackingAsync();
        var distinctContracts = await query
            .Select(m => m.MessageContract)
            .Where(contract => !string.IsNullOrEmpty(contract))
            .Distinct()
            .OrderBy(contract => contract)
            .ToListAsync();

        _cache.Set(cacheKey, distinctContracts, TimeSpan.FromMinutes(5));
        
        return distinctContracts;
    }

    public async Task<List<string>> GetDistinctEndpointsAsync()
    {
        const string cacheKey = "mirage-distinct-endpoints";
        
        if (_cache.TryGetValue(cacheKey, out List<string>? cachedEndpoints) && cachedEndpoints != null)
        {
            return cachedEndpoints;
        }

        var query = await _outboundRepository.GetAllNoTrackingAsync();
        var distinctEndpoints = await query
            .Select(m => m.ConsumerEndpoint)
            .Where(endpoint => !string.IsNullOrEmpty(endpoint))
            .Distinct()
            .OrderBy(endpoint => endpoint)
            .ToListAsync();

        _cache.Set(cacheKey, distinctEndpoints, TimeSpan.FromMinutes(5));
        
        return distinctEndpoints;
    }
}
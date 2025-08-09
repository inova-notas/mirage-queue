using Microsoft.AspNetCore.Mvc;
using MirageQueue.Dashboard.Services;

namespace MirageQueue.Dashboard.Controllers;

[Area("MirageDashboard")]
[Route("mirage-dashboard")]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [Route("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Dashboard Overview";
        return View();
    }

    [Route("stats")]
    public async Task<IActionResult> Stats()
    {
        var stats = await _dashboardService.GetDashboardStatsAsync();
        return PartialView("_Stats", stats);
    }

    [Route("messages")]
    public async Task<IActionResult> Messages(string type = "inbound", int page = 1, int pageSize = 20, string? statusFilter = null, string? contractFilter = null, string? endpointFilter = null)
    {
        ViewData["Title"] = $"{char.ToUpper(type[0])}{type[1..]} Messages";
        
        var viewModel = type.ToLower() switch
        {
            "inbound" => await _dashboardService.GetInboundMessagesAsync(page, pageSize, statusFilter),
            "outbound" => await _dashboardService.GetOutboundMessagesAsync(page, pageSize, statusFilter, contractFilter, endpointFilter),
            "scheduled" => await _dashboardService.GetScheduledMessagesAsync(page, pageSize, statusFilter),
            _ => await _dashboardService.GetInboundMessagesAsync(page, pageSize, statusFilter)
        };

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_MessagesList", viewModel);
        }

        return View("Messages", viewModel);
    }

    [Route("message/{id:guid}")]
    public async Task<IActionResult> MessageDetail(Guid id, string type = "inbound")
    {
        var message = type.ToLower() switch
        {
            "inbound" => await _dashboardService.GetInboundMessageDetailAsync(id),
            "outbound" => await _dashboardService.GetOutboundMessageDetailAsync(id),
            "scheduled" => await _dashboardService.GetScheduledMessageDetailAsync(id),
            _ => null
        };

        if (message == null)
        {
            return NotFound();
        }

        ViewData["Title"] = $"Message Detail - {message.MessageType}";
        return View("MessageDetail", message);
    }

    [Route("requeue/{id:guid}")]
    [HttpPost]
    public async Task<IActionResult> Requeue(Guid id, string type = "inbound")
    {
        var success = type.ToLower() switch
        {
            "inbound" => await _dashboardService.RequeueInboundMessageAsync(id),
            "outbound" => await _dashboardService.RequeueOutboundMessageAsync(id),
            "scheduled" => await _dashboardService.RequeueScheduledMessageAsync(id),
            _ => false
        };

        if (success)
        {
            TempData["SuccessMessage"] = "Message requeued successfully!";
        }
        else
        {
            TempData["ErrorMessage"] = "Failed to requeue message.";
        }

        return RedirectToAction("MessageDetail", new { id, type });
    }
}
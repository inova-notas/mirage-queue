using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MirageQueue.Dashboard.Services;

namespace MirageQueue.Dashboard;

public static class DashboardExtensions
{
    /// <summary>
    /// Adds MirageQueue Dashboard services to the service collection.
    /// This includes the dashboard service and all required dependencies.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="routePrefix">Optional route prefix for dashboard endpoints (default: "mirage-dashboard")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMirageQueueDashboard(this IServiceCollection services, string routePrefix = "mirage-dashboard")
    {
        // Register dashboard service
        services.AddScoped<IDashboardService, DashboardService>();

        // Add MVC with areas support for embedded views
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(DashboardExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Maps MirageQueue Dashboard routes to the application.
    /// Call this in your endpoint configuration (typically in Program.cs after UseRouting()).
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <param name="routePrefix">Optional route prefix for dashboard endpoints (default: "mirage-dashboard")</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseMirageQueueDashboard(this IApplicationBuilder app, string routePrefix = "mirage-dashboard")
    {
        // This method now just configures routing - the actual mapping should happen in endpoint configuration
        return app;
    }

    /// <summary>
    /// Maps MirageQueue Dashboard endpoints to the endpoint route builder.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <param name="routePrefix">Optional route prefix for dashboard endpoints (default: "mirage-dashboard")</param>
    public static void MapMirageQueueDashboard(this IEndpointRouteBuilder endpoints, string routePrefix = "mirage-dashboard")
    {
        // Ensure the route prefix doesn't start with a slash
        routePrefix = routePrefix.TrimStart('/');

        // Map area routes for dashboard
        endpoints.MapAreaControllerRoute(
            name: "mirage-dashboard-area",
            areaName: "MirageDashboard",
            pattern: $"{routePrefix}/{{controller=Dashboard}}/{{action=Index}}/{{id?}}");
    }
}
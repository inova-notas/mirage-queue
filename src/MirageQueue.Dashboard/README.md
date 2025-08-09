# MirageQueue Dashboard

A web-based dashboard for monitoring and managing MirageQueue message queues, similar to Hangfire's dashboard.

## Features

- **Real-time Statistics**: View live stats for inbound, outbound, and scheduled messages with auto-refresh
- **Message Management**: Browse, filter, and search through all message types with pagination
- **Advanced Filtering**: Filter outbound messages by contract and endpoint with cached dropdown options
- **Interactive Tooltips**: Hover over message content to see full payload with JSON prettification
- **Message Details**: View complete message information with proper formatting
- **Requeue Functionality**: Requeue failed or processed messages with one click
- **Dark/Light Theme Toggle**: Switch between themes with persistent storage
- **Responsive Design**: Modern UI built with Bootstrap 5 that works on all devices
- **Real-time Updates**: Auto-refreshing dashboard using HTMX without full page reloads

## Installation

Install the package via NuGet:

```bash
dotnet add package InovaNotas.MirageQueue.Dashboard
```

## Usage

Add the dashboard to your ASP.NET Core application:

```csharp
using MirageQueue;
using MirageQueue.Postgres;
using MirageQueue.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Configure MirageQueue
builder.Services.AddMirageQueue();
builder.Services.AddMirageQueuePostgres(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddConsumersFromAssembly(typeof(Program).Assembly);

// Add dashboard
builder.Services.AddMirageQueueDashboard();

var app = builder.Build();

app.UseRouting();

// Map dashboard endpoints
app.MapMirageQueueDashboard();

// Initialize MirageQueue (must be after routing)
app.UseMirageQueue();

app.Run();
```

## Accessing the Dashboard

By default, the dashboard is available at `/mirage-dashboard`. You can customize the route prefix:

```csharp
// Custom route prefix
app.MapMirageQueueDashboard("my-queue-dashboard");
// Accessible at: https://your-app/my-queue-dashboard
```

## Dashboard Sections

### Overview
- Real-time statistics for all message types
- Quick navigation to message lists
- System status information

### Messages
- **Inbound Messages**: Messages received for processing
- **Outbound Messages**: Messages being sent to external endpoints
  - Contract filtering (cached dropdown)
  - Endpoint filtering (cached dropdown) 
- **Scheduled Messages**: Messages scheduled for future processing
- Status filtering for all message types
- Pagination with configurable page sizes
- Hover tooltips showing full message payload

### Message Details
- Complete message information with JSON formatting
- Message history and timestamps
- Consumer endpoint information (for outbound messages)
- Execute time information (for scheduled messages)
- Requeue functionality for eligible messages

## Security Considerations

The dashboard does not include built-in authentication. In production environments, you should:

1. Secure the dashboard route with your authentication middleware
2. Restrict access to authorized users only
3. Consider hosting it on a separate port or subdomain

Example with authentication:

```csharp
app.MapMirageQueueDashboard()
   .RequireAuthorization("AdminPolicy");
```

## Customization

The dashboard uses modern CSS frameworks and can be customized by:
- Overriding CSS variables for theming
- Using the included theme switcher
- Customizing the route patterns

## Dependencies

- ASP.NET Core 9.0+
- InovaNotas.MirageQueue 2.2.0+
- Bootstrap 5.3+ (loaded from CDN)
- FontAwesome 6.4+ (loaded from CDN)
- Bootstrap Icons (loaded from CDN)
- HTMX 1.9+ (loaded from CDN)

All external dependencies are loaded from CDN, so no additional packages are required.

## Troubleshooting

### Common Issues

#### Dashboard not accessible
- **Issue**: 404 error when accessing `/mirage-dashboard`
- **Solution**: Ensure you called `app.MapMirageQueueDashboard()` after `app.UseRouting()`

```csharp
app.UseRouting();
app.MapMirageQueueDashboard(); // Must be after UseRouting()
```

#### No data showing in dashboard
- **Issue**: Dashboard loads but shows no messages or statistics
- **Solutions**:
  1. Verify your database connection string is correct
  2. Ensure `app.UseMirageQueue()` is called to create database tables
  3. Check that messages are being published to the queue

#### CSS/JavaScript not loading
- **Issue**: Dashboard appears unstyled or interactive features don't work
- **Solution**: Ensure your application can access external CDNs. For offline environments, consider hosting the assets locally.

#### Tooltips not working
- **Issue**: Hover tooltips for message content not appearing
- **Solutions**:
  1. Clear browser cache and hard refresh (Ctrl+F5)
  2. Check browser console for JavaScript errors
  3. Ensure HTMX is loading properly

#### Filtering not working
- **Issue**: Contract/Endpoint dropdowns empty or filtering doesn't work
- **Solutions**:
  1. Ensure you have outbound messages in your database
  2. Check that message contracts and endpoints are not null/empty
  3. Wait for cache refresh (5-minute cache timeout)

#### Performance issues
- **Issue**: Dashboard loads slowly or times out
- **Solutions**:
  1. Reduce page size in messages view
  2. Consider database indexing on frequently queried columns
  3. Monitor database connection pool usage

### Development Tips

1. **Use Debug Configuration**: During development, the dashboard automatically uses local project references for easier debugging

2. **Check Application Logs**: Enable detailed logging to troubleshoot issues:
```csharp
builder.Services.AddLogging(builder => 
    builder.SetMinimumLevel(LogLevel.Debug));
```

3. **Verify Dependencies**: Ensure all required services are registered:
```csharp
// Required for MirageQueue
builder.Services.AddMirageQueue();
builder.Services.AddMirageQueuePostgres(connectionString);

// Required for Dashboard
builder.Services.AddMirageQueueDashboard();
```

### Best Practices

1. **Secure in Production**: Always add authentication/authorization:
```csharp
app.MapMirageQueueDashboard()
   .RequireAuthorization("AdminPolicy");
```

2. **Custom Route**: Use descriptive route prefixes:
```csharp
app.MapMirageQueueDashboard("admin/queues");
```

3. **Monitoring**: Consider logging dashboard access for security auditing

4. **Performance**: For high-volume systems, consider:
   - Separate read-only database connections for dashboard
   - Implementing custom caching strategies
   - Limiting dashboard access during peak hours

For more help, check the [main project README](../../README.md) or create an issue in the GitHub repository.
# MirageQueue Dashboard

A web-based dashboard for monitoring and managing MirageQueue message queues, similar to Hangfire's dashboard.

## Features

- **Real-time Statistics**: View live stats for inbound, outbound, and scheduled messages
- **Message Management**: Browse, filter, and search through all message types
- **Message Details**: View detailed information about individual messages
- **Requeue Functionality**: Requeue failed or processed messages
- **Responsive Design**: Modern UI built with Bootstrap 5 and DaisyUI
- **Real-time Updates**: Auto-refreshing dashboard using HTMX

## Installation

Install the package via NuGet:

```bash
dotnet add package InovaNotas.MirageQueue.Dashboard
```

## Usage

Add the dashboard to your ASP.NET Core application:

```csharp
using MirageQueue.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add your MirageQueue services
builder.Services.AddMirageQueue();
builder.Services.AddMirageQueuePostgres(connectionString);

// Add the dashboard (optional)
builder.Services.AddMirageQueueDashboard();

var app = builder.Build();

app.UseRouting();

// Map dashboard endpoints
app.MapMirageQueueDashboard();

app.Run();
```

## Accessing the Dashboard

By default, the dashboard is available at `/mirage-dashboard`. You can customize the route prefix:

```csharp
// Custom route prefix
builder.Services.AddMirageQueueDashboard();
app.MapMirageQueueDashboard("my-queue-dashboard");
```

## Dashboard Sections

### Overview
- Real-time statistics for all message types
- Quick navigation to message lists
- System status information

### Messages
- Paginated lists of inbound, outbound, and scheduled messages
- Status filtering
- Message search and navigation

### Message Details
- Complete message information including content
- Message history and timestamps
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
- MirageQueue core library
- Bootstrap 5 (CDN)
- DaisyUI (CDN)
- FontAwesome (CDN)
- HTMX (CDN)
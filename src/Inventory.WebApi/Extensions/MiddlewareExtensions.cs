

using Inventory.WebApi.Middleware;

namespace Inventory.WebApi.Extensions;

public static class MiddlewareExtensions
{   
    public static IApplicationBuilder UseRequestCorrelationId(this IApplicationBuilder app)
    {
        app.UseMiddleware<TracingMiddleware>();
        return app;
    }

    public static IApplicationBuilder UseRequestContextLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestContextLoggingMiddleware>();

        return app;
    }
}

using Nur.Store2026.Observability.Tracing;
using Serilog.Context;

namespace Inventory.WebApi.Middleware;

public class RequestContextLoggingMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context, ITracingProvider tracingProvider)
    {
        using (LogContext.PushProperty("CorrelationId", tracingProvider.GetCorrelationId()))

        using (LogContext.PushProperty("TraceId", tracingProvider.GetTraceId()))
            
            return next.Invoke(context);

    }
}

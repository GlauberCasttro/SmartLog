using Microsoft.AspNetCore.Http;
using SmartLog.Core.Service;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.CustomMiddleware;

/// <summary>
/// SmartLogMiddleware responsável por registrar logs https
/// </summary>
/// <param name="next"></param>
/// <param name="registry"></param>
/// <param name="detector"></param>
[ExcludeFromCodeCoverage]
public class SmartLogMiddleware(RequestDelegate next, MetricsRegistry registry)
{
    private readonly RequestDelegate _next = next;
    private readonly MetricsRegistry _registry = registry;

    public async Task InvokeAsync(HttpContext context)
    {
        // Ignora o health check da própria SDK para não poluir as métricas
        if (context.Request.Path.StartsWithSegments("/api/smart-logs"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
        
            // Erros de servidor (5xx) são considerados para a métrica de erro.
            bool isHttpError = context.Response.StatusCode >= 500;
            _registry.AddRequestMetric((int)stopwatch.ElapsedMilliseconds, isHttpError);
        }
    }
}
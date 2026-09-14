using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Amaliyotchi.Api.Infrastructure;

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

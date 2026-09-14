using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Api.Infrastructure;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>API bilan bir xil JSON kelishuvi (camelCase, enum string) — deserializatsiya shu bilan.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = JsonConventions.Create();

    public static Task<T?> ReadAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(Options, cancellationToken);

    public static Task<HttpResponseMessage> PostJsonAsync<T>(
        this HttpClient client, string url, T body, CancellationToken cancellationToken = default) =>
        client.PostAsJsonAsync(url, body, Options, cancellationToken);
}

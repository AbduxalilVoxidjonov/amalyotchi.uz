using Amaliyotchi.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amaliyotchi.IntegrationTests.Unit;

/// <summary>Production xavfsizligi: kutilmagan xato ichki tafsilotsiz 500, Kestrel so'rov xatolari — o'z status kodi bilan.</summary>
public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, string Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);
        handled.Should().BeTrue();

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task KutilmaganXato_500_IchkiXabarVaStackTraceChiqmaydi()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("Host=postgres;Password=sir"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Should().NotContain("Password=sir").And.NotContain("InvalidOperationException").And.Contain("traceId");
    }

    [Theory]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    [InlineData(StatusCodes.Status400BadRequest)]
    public async Task BadHttpRequestException_OzStatusKodi_500Emas(int status)
    {
        var (actual, _) = await HandleAsync(new BadHttpRequestException("limit", status));
        actual.Should().Be(status);
    }

    [Fact]
    public async Task OralganBadHttpRequestException_OzStatusKodi()
    {
        var (actual, _) = await HandleAsync(
            new InvalidOperationException("wrap", new BadHttpRequestException("limit", StatusCodes.Status413PayloadTooLarge)));
        actual.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }
}

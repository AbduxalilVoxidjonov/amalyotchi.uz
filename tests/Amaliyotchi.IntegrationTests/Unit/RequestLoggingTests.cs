using Amaliyotchi.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace Amaliyotchi.IntegrationTests.Unit;

/// <summary>Request log darajasi haqiqiy javob statusiga qarab: 4xx domen xatolari ERR emas.</summary>
public sealed class RequestLoggingTests
{
    private static LogEventLevel Level(int status, Exception? exception = null) =>
        RequestLogging.GetLevel(new DefaultHttpContext { Response = { StatusCode = status } }, 1, exception);

    [Theory]
    [InlineData(200, LogEventLevel.Information)]
    [InlineData(204, LogEventLevel.Information)]
    [InlineData(400, LogEventLevel.Warning)]
    [InlineData(401, LogEventLevel.Warning)]
    [InlineData(403, LogEventLevel.Warning)]
    [InlineData(404, LogEventLevel.Warning)]
    [InlineData(409, LogEventLevel.Warning)]
    [InlineData(499, LogEventLevel.Warning)]
    [InlineData(500, LogEventLevel.Error)]
    [InlineData(503, LogEventLevel.Error)]
    public void Daraja_StatusBoyicha(int status, LogEventLevel expected) =>
        Level(status).Should().Be(expected);

    [Theory]
    [InlineData(200, LogEventLevel.Verbose)]
    [InlineData(503, LogEventLevel.Error)]
    public void Health_MuvaffaqiyatliJavobShovqinEmas(int status, LogEventLevel expected) =>
        RequestLogging.GetLevel(
                new DefaultHttpContext { Request = { Path = "/health" }, Response = { StatusCode = status } }, 1, null)
            .Should().Be(expected);

    [Fact]
    public void IshlovBerilmaganException_Error() =>
        Level(200, new InvalidOperationException()).Should().Be(LogEventLevel.Error);
}

using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ValidationException = Amaliyotchi.Application.Common.Exceptions.ValidationException;

namespace Amaliyotchi.Api.Infrastructure;

/// <summary>Barcha xatolar bitta shaklda (RFC 7807) chiqadi. Stack trace hech qachon
/// tashqariga chiqmaydi; foydalanuvchi traceId ni aytsa, log'dan topiladi.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Nginx uslubidagi "Client Closed Request" — faqat log uchun.</summary>
    private const int StatusClientClosedRequest = 499;

    private const string ProblemContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        // Klient ketgan bo'lsa javob yozishning ma'nosi yo'q. Bu holatni odatda
        // ExceptionHandlerMiddleware o'zi ushlaydi (handler'ga yetib kelmaydi); bu — himoya tarmog'i.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("So'rov klient tomonidan bekor qilindi ({Status}) traceId={TraceId}",
                StatusClientClosedRequest, traceId);
            return true;
        }

        var problem = exception switch
        {
            ValidationException validation => Validation(validation),
            ServiceUnavailableException unavailable => Problem(StatusCodes.Status503ServiceUnavailable, "Xizmat vaqtincha ishlamayapti", unavailable.Message),
            UnauthorizedException unauthorized => Problem(StatusCodes.Status401Unauthorized, "Avtorizatsiya talab qilinadi", unauthorized.Message),
            NotFoundException notFound => Problem(StatusCodes.Status404NotFound, "Topilmadi", notFound.Message),
            ConflictException conflict => Problem(StatusCodes.Status409Conflict, "Ziddiyat", conflict.Message),
            ForbiddenException forbidden => Problem(StatusCodes.Status403Forbidden, "Ruxsat yo'q", forbidden.Message),
            DomainException domain => Problem(StatusCodes.Status400BadRequest, "Noto'g'ri amal", domain.Message),
            OperationCanceledException => Problem(StatusClientClosedRequest, "So'rov bekor qilindi", "So'rov bekor qilindi."),
            // Kestrel/ASP.NET so'rov xatolari (tana hajmi chegaradan oshdi — 413, buzuq so'rov — 400 ...): 500 emas,
            // o'z status kodi bilan. Fayl yuklashda limitdan oshsa mijoz aniq 413 oladi.
            BadHttpRequestException badRequest => BadRequest(badRequest.StatusCode),
            // MVC forma o'qishdagi xatoni ValueProviderException ichiga o'raydi.
            { InnerException: BadHttpRequestException inner } => BadRequest(inner.StatusCode),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Ziddiyat", "Ma'lumot boshqa foydalanuvchi tomonidan o'zgartirilgan. Sahifani yangilab, qayta urinib ko'ring."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                Problem(StatusCodes.Status409Conflict, "Ziddiyat", "Bunday yozuv allaqachon mavjud."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                Problem(StatusCodes.Status409Conflict, "Ziddiyat", "Yozuv boshqa ma'lumotlarga bog'langan yoki bog'lanayotgan ma'lumot mavjud emas."),
            _ => Problem(
                StatusCodes.Status500InternalServerError,
                "Ichki xatolik",
                "Kutilmagan xatolik yuz berdi. Iltimos, qayta urinib ko'ring.")
        };

        // Domain xatosining mashina o'qiydigan maydonlari (masalan check-in rad etilganda rejectReason).
        if (exception is DomainException { Extensions.Count: > 0 } withExtensions)
        {
            foreach (var (key, value) in withExtensions.Extensions)
                problem.Extensions[key] = value;
        }

        problem.Extensions["traceId"] = traceId;

        if (problem.Status == StatusCodes.Status503ServiceUnavailable)
            logger.LogWarning("Xizmat ishlamayapti: {Detail} traceId={TraceId}", problem.Detail, traceId);
        else if (problem.Status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Ishlov berilmagan xatolik. traceId={TraceId}", traceId);
        else
            logger.LogInformation("Xatolik: {Title} ({Status}) traceId={TraceId}", problem.Title, problem.Status, traceId);

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        // ProblemDetails yozish qisqa; so'rov tokeni bekor qilingan bo'lsa ham javobni tugatamiz.
        // Content-Type — kontrakt bo'yicha application/problem+json (mijoz ApiError shu bo'yicha ajratadi).
        await httpContext.Response.WriteAsJsonAsync(
            problem, options: null, contentType: ProblemContentType, CancellationToken.None);

        return true;
    }

    private static ProblemDetails BadRequest(int status) => status == StatusCodes.Status413PayloadTooLarge
        ? Problem(status, "So'rov juda katta", "Yuborilgan ma'lumot (fayl) hajmi ruxsat etilgan chegaradan oshdi.")
        : Problem(status, "Noto'g'ri so'rov", "So'rovni qayta ishlab bo'lmadi.");

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails Validation(ValidationException exception)
    {
        var problem = Problem(
            StatusCodes.Status400BadRequest,
            "Ma'lumotlar noto'g'ri",
            exception.Message);

        problem.Extensions["errors"] = exception.Errors;
        return problem;
    }
}

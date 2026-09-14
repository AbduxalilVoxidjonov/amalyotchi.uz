namespace Amaliyotchi.Application.Common.Exceptions;

/// <summary>Kim ekani noma'lum (token yo'q yoki yaroqsiz). API qatlamida 401 ga aylanadi.
/// Kim ekani ma'lum, lekin ruxsati yo'q holat uchun <c>ForbiddenException</c> (403) ishlatiladi.</summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Avtorizatsiya talab qilinadi.")
        : base(message) { }
}

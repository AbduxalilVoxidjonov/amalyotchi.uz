namespace Amaliyotchi.Application.Common.Exceptions;

/// <summary>FluentValidation natijasini bitta shaklga keltiradi —
/// API qatlami uni 400 + errors lug'atiga aylantiradi.</summary>
public sealed class ValidationException : Exception
{
    /// <param name="message">ProblemDetails <c>detail</c> matni; berilmasa — umumiy matn.
    /// Bitta aniq sabab bo'lganda (masalan "Check-in uchun rasm majburiy.") foydalanuvchiga shu ko'rsatiladi.</param>
    public ValidationException(IDictionary<string, string[]> errors, string? message = null)
        : base(message ?? "Kiritilgan ma'lumotlarda xatolik bor.")
        => Errors = errors;

    public IDictionary<string, string[]> Errors { get; }
}

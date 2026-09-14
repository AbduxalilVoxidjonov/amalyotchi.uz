namespace Amaliyotchi.Application.Common.Exceptions;

/// <summary>FluentValidation natijasini bitta shaklga keltiradi —
/// API qatlami uni 400 + errors lug'atiga aylantiradi.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("Kiritilgan ma'lumotlarda xatolik bor.")
        => Errors = errors;

    public IDictionary<string, string[]> Errors { get; }
}

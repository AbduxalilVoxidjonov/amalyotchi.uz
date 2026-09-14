namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Vaqt doimo UTC'da olinadi. Test uchun almashtiriladi —
/// handler'larda DateTimeOffset.UtcNow to'g'ridan-to'g'ri chaqirilmaydi.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

namespace Amaliyotchi.Application.Common.Exceptions;

/// <summary>Kerakli ichki xizmat (masalan yuzni tanish modellari) hozir ishlamayapti. API qatlamida 503 ga aylanadi —
/// so'rov jimgina "o'tkazib yuborilmaydi".</summary>
public sealed class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message) : base(message) { }
}

namespace Amaliyotchi.Domain.Exceptions;

/// <summary>Domain qoidasi buzilganda. API qatlamida 400/409 ga aylanadi.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} topilmadi (id: {key}).") { }

    public NotFoundException(string message) : base(message) { }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Bu amal uchun ruxsatingiz yo'q.")
        : base(message) { }
}

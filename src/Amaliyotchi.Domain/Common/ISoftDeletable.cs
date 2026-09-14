namespace Amaliyotchi.Domain.Common;

/// <summary>Ma'lumot hech qachon jismonan o'chirilmaydi — arxivlanadi.
/// Global query filter shu bayroqqa tayanadi.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Settings;

/// <summary>Yuklab olinadigan hujjat shabloni (fayl <c>StoredFile</c> da).</summary>
public sealed class DocumentTemplate : AuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;

    private DocumentTemplate() { }

    public string Name { get; private set; } = string.Empty;
    public DocumentTemplateKind Kind { get; private set; }
    public Guid FileId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static DocumentTemplate Create(string name, DocumentTemplateKind kind, Guid fileId)
    {
        var template = new DocumentTemplate { Kind = kind, IsActive = true };
        template.Rename(name);
        template.ReplaceFile(fileId);
        return template;
    }

    public void Rename(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Shablon nomi bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"Shablon nomi {NameMaxLength} belgidan oshmasligi kerak.");
        Name = trimmed;
    }

    public void ReplaceFile(Guid fileId)
    {
        if (fileId == Guid.Empty)
            throw new DomainException("Shablon fayli ko'rsatilmagan.");
        FileId = fileId;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

namespace Amaliyotchi.Domain.Common;

/// <summary>Barcha entity'lar uchun umumiy asos. Id — UUIDv7: vaqt bo'yicha tartiblangan,
/// shuning uchun PostgreSQL indeksida tasodifiy GUID'dan ancha yaxshi ishlaydi.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public override bool Equals(object? obj) =>
        obj is BaseEntity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

namespace Amaliyotchi.Application.Common.Scoping;

/// <summary>Joriy foydalanuvchi ko'ra oladigan talabalar to'plami.
/// Admin — cheklovsiz; talaba — faqat o'zi; boshqa/anonim — bo'sh.</summary>
public sealed record DataScope(ScopeKind Kind, Guid? UserId)
{
    public static DataScope Unrestricted(Guid userId) => new(ScopeKind.Unrestricted, userId);

    public static DataScope ForStudent(Guid studentUserId) => new(ScopeKind.Self, studentUserId);

    public static DataScope Empty { get; } = new(ScopeKind.None, null);

    public bool IsUnrestricted => Kind == ScopeKind.Unrestricted;

    /// <summary>Talaba (UserId bo'yicha) ko'lamga kiradimi — xotiradagi tekshiruv.</summary>
    public bool Includes(Guid studentUserId) => Kind switch
    {
        ScopeKind.Unrestricted => true,
        ScopeKind.Self => UserId == studentUserId,
        _ => false
    };
}

/// <summary>Raqamlar tarixiy: <c>2</c> — olib tashlangan tyutor ko'lami (guruhlar).</summary>
public enum ScopeKind
{
    None = 0,
    /// <summary>Admin — barcha talabalar.</summary>
    Unrestricted = 1,
    /// <summary>Talaba — faqat o'zi.</summary>
    Self = 3
}

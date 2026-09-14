namespace Amaliyotchi.Application.Common.Scoping;

/// <summary>Joriy foydalanuvchi ko'ra oladigan talabalar to'plami.
/// Admin — cheklovsiz; tyutor — faol <c>TutorAssignment</c> bo'yicha guruhlar (va ulardagi talabalar);
/// talaba — faqat o'zi; boshqa/anonim — bo'sh.</summary>
public sealed record DataScope(
    ScopeKind Kind,
    Guid? UserId,
    IReadOnlyList<Guid> StudentGroupIds,
    IReadOnlyList<Guid> StudentUserIds)
{
    public static DataScope Unrestricted(Guid userId) => new(ScopeKind.Unrestricted, userId, [], []);

    public static DataScope ForTutor(Guid tutorUserId, IReadOnlyList<Guid> groupIds, IReadOnlyList<Guid> studentUserIds)
        => new(ScopeKind.Groups, tutorUserId, groupIds, studentUserIds);

    public static DataScope ForStudent(Guid studentUserId) => new(ScopeKind.Self, studentUserId, [], [studentUserId]);

    public static DataScope Empty { get; } = new(ScopeKind.None, null, [], []);

    public bool IsUnrestricted => Kind == ScopeKind.Unrestricted;

    /// <summary>Talaba (UserId bo'yicha) ko'lamga kiradimi — xotiradagi tekshiruv.</summary>
    public bool Includes(Guid studentUserId) => Kind switch
    {
        ScopeKind.Unrestricted => true,
        ScopeKind.Groups => StudentUserIds.Contains(studentUserId),
        ScopeKind.Self => UserId == studentUserId,
        _ => false
    };
}

public enum ScopeKind
{
    None = 0,
    /// <summary>Admin — barcha talabalar.</summary>
    Unrestricted = 1,
    /// <summary>Tyutor — biriktirilgan guruhlar.</summary>
    Groups = 2,
    /// <summary>Talaba — faqat o'zi.</summary>
    Self = 3
}

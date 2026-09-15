namespace Amaliyotchi.Domain.Students;

/// <summary>Tyutor ko'lamining darajasi — ierarxiyada qaysi tugun tanlangani. Raqamlar bazada saqlanadi —
/// o'zgartirilmaydi. Tartib muhim: kichik raqam — kengroq ko'lam (<see cref="TutorScope.Overlaps"/> shunga tayanadi).</summary>
public enum TutorScopeLevel
{
    Faculty = 1,
    Department = 2,
    Direction = 3,
    Group = 4
}

namespace Amaliyotchi.Domain.Students;

/// <summary>Talabaning akademik holati. Raqamlar bazada saqlanadi — o'zgartirilmaydi.</summary>
public enum StudentStatus
{
    Active = 1,
    /// <summary>Akademik ta'til yoki vaqtincha chetlashtirilgan — amaliyotga chiqmaydi.</summary>
    Suspended = 2,
    Graduated = 3
}

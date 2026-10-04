namespace Amaliyotchi.Domain.Enums;

/// <summary>Foydalanuvchi roli. Raqamlar bazada saqlanadi — o'zgartirilmaydi. <c>2</c> — tarixiy "tyutor" roli
/// (olib tashlangan): bunday yozuvlar migratsiyada soft-delete qilingan va kod ularni o'qimaydi/ko'rsatmaydi.</summary>
public enum UserRole
{
    Admin = 1,
    Student = 3
}

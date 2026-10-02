using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary>Xabar qabul qiluvchilar manbasi: Telegram ulangan (<c>TelegramUserId != null</c>), faol, o'chirilmagan talabalar
/// (soft-delete filtri). Profil → guruh → yo'nalish → kafedra → fakultet zanjiri LEFT JOIN — zanjiri to'liq bo'lmagan
/// talaba ham ro'yxatda (akademik maydonlari null), lekin fakultet/yo'nalish/guruh/kurs filtriga tushmaydi.
/// Ro'yxat (<see cref="GetMessageRecipientsQuery"/>) va auditoriyani resolve qilish (<see cref="CreateMessageCommand"/>)
/// AYNAN shu manba va filtrdan foydalanadi — admin ko'rgan ro'yxat bilan yuborilgan ro'yxat bir xil.</summary>
internal static class MessageRecipientQueries
{
    public static IQueryable<RecipientSource> Source(IApplicationDbContext db)
        => from u in db.Users.AsNoTracking()
           where u.Role == UserRole.Student && u.IsActive && u.TelegramUserId != null
           join p in db.StudentProfiles on u.Id equals p.UserId into profiles
           from p in profiles.DefaultIfEmpty()
           join g in db.StudentGroups on p.StudentGroupId equals g.Id into groups
           from g in groups.DefaultIfEmpty()
           join d in db.Directions on g.DirectionId equals d.Id into directions
           from d in directions.DefaultIfEmpty()
           join dept in db.Departments on d.DepartmentId equals dept.Id into departments
           from dept in departments.DefaultIfEmpty()
           join f in db.Faculties on dept.FacultyId equals f.Id into faculties
           from f in faculties.DefaultIfEmpty()
           select new RecipientSource { User = u, Profile = p, Group = g, Direction = d, Faculty = f };

    /// <summary>Filtrlar AND bilan. <paramref name="q"/>: FISH yoki HEMIS ID (qism, case-insensitive) yoki Telegram ID (aniq).</summary>
    public static IQueryable<RecipientSource> Filter(
        IQueryable<RecipientSource> source, string? q, Guid? facultyId, Guid? directionId, Guid? groupId, int? course)
    {
        if (facultyId is { } faculty)
            source = source.Where(x => x.Faculty != null && x.Faculty.Id == faculty);
        if (directionId is { } direction)
            source = source.Where(x => x.Direction != null && x.Direction.Id == direction);
        if (groupId is { } group)
            source = source.Where(x => x.Group != null && x.Group.Id == group);
        if (course is { } c)
            source = source.Where(x => x.Group != null && x.Group.Course == c);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = AdminSearch.Pattern(q);
            long? telegramId = long.TryParse(q.Trim(), out var parsed) ? parsed : null;
            source = source.Where(x =>
                EF.Functions.Like(x.User.FullName.ToLower(), pattern, AdminSearch.Escape)
                || (x.Profile != null && EF.Functions.Like(x.Profile.HemisId, pattern, AdminSearch.Escape))
                || (telegramId != null && x.User.TelegramUserId == telegramId));
        }

        return source;
    }
}

/// <summary>EF kompozitsiyasi uchun init-xossali tur (LEFT JOIN — navigatsiyalar null bo'lishi mumkin).</summary>
internal sealed class RecipientSource
{
    public required User User { get; init; }
    public StudentProfile? Profile { get; init; }
    public StudentGroup? Group { get; init; }
    public Direction? Direction { get; init; }
    public Faculty? Faculty { get; init; }
}

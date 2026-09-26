using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Practice;

/// <summary>Amaliyot davri: sanalar, kunlik ish vaqti, oynalar va biriktirilgan guruhlar.
/// Vaqt parametrlari yaratilishda global sozlamalardan nusxalanadi — sozlama keyin o'zgarsa
/// ketayotgan davr qoidalari o'zgarib ketmaydi.</summary>
public sealed class PracticePeriod : AuditableEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;

    private readonly List<PracticePeriodGroup> _groups = [];

    private PracticePeriod() { }

    public string Name { get; private set; } = string.Empty;
    public Guid AcademicYearId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    /// <summary>Kunlik ish boshlanishi (Toshkent), odatda 09:00 — check-in oynasi shu paytdan ochiladi.</summary>
    public TimeOnly DailyStart { get; private set; }

    /// <summary>Kunlik ish tugashi, odatda 17:00 — check-out shu paytdan ruxsat etiladi.</summary>
    public TimeOnly DailyEnd { get; private set; }

    /// <summary>Shu daqiqadan keyin check-in "kech keldi" (09:15).</summary>
    public int LateToleranceMinutes { get; private set; }

    /// <summary>Shu daqiqadan keyin check-in qabul qilinmaydi — "kelmadi" (10:30).</summary>
    public int CheckInWindowMinutes { get; private set; }

    /// <summary>Ish tugagach shuncha daqiqa ichida check-out qilinmasa kun avtomatik yopiladi (18:00).</summary>
    public int CheckoutGraceMinutes { get; private set; }

    public WorkDays WorkDays { get; private set; }

    /// <summary>Amaliyot uchun talab qilinadigan ish kunlari soni (hisobot/baholash uchun).</summary>
    public int RequiredDays { get; private set; }
    public bool DailyReportRequired { get; private set; }
    public PracticePeriodStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public IReadOnlyCollection<PracticePeriodGroup> Groups => _groups.AsReadOnly();

    public static PracticePeriod Create(
        string name,
        Guid academicYearId,
        DateOnly startDate,
        DateOnly endDate,
        Guid createdByUserId,
        CheckInRules rules,
        WorkDays workDays,
        int requiredDays,
        bool dailyReportRequired)
    {
        var trimmed = NormalizeName(name);
        if (academicYearId == Guid.Empty)
            throw new DomainException("O'quv yili ko'rsatilmagan.");
        if (endDate < startDate)
            throw new DomainException("Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        if (workDays == WorkDays.None)
            throw new DomainException("Kamida bitta ish kuni belgilanishi kerak.");
        if (requiredDays < 0)
            throw new DomainException("Talab qilinadigan kunlar soni manfiy bo'lishi mumkin emas.");
        ArgumentNullException.ThrowIfNull(rules);

        return new PracticePeriod
        {
            Name = trimmed,
            AcademicYearId = academicYearId,
            StartDate = startDate,
            EndDate = endDate,
            DailyStart = rules.DailyStart,
            DailyEnd = rules.DailyEnd,
            LateToleranceMinutes = rules.LateToleranceMinutes,
            CheckInWindowMinutes = rules.CheckInWindowMinutes,
            CheckoutGraceMinutes = rules.CheckoutGraceMinutes,
            WorkDays = workDays,
            RequiredDays = requiredDays,
            DailyReportRequired = dailyReportRequired,
            Status = PracticePeriodStatus.Planned,
            CreatedByUserId = createdByUserId
        };
    }

    /// <summary>Davrning vaqt qoidalari. GPS aniqlik chegarasi davrga emas, global sozlamaga tegishli — parametr.</summary>
    public CheckInRules Rules(double minAccuracyM)
        => new(DailyStart, DailyEnd, LateToleranceMinutes, CheckInWindowMinutes, CheckoutGraceMinutes, minAccuracyM);

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    /// <summary>Kun ish kunimi: davr ichida, hafta kuni bo'yicha va bayram emas.</summary>
    public bool IsWorkDay(DateOnly date, bool isHoliday) => Contains(date) && WorkDays.Includes(date) && !isHoliday;

    public bool IncludesGroup(Guid studentGroupId) => _groups.Any(g => g.StudentGroupId == studentGroupId);

    /// <summary>Davr ochiqmi (yopilmagan). Saqlanadigan <see cref="Status"/> — davrning hayot sikli:
    /// admin yaratgan davr darhol <see cref="PracticePeriodStatus.Active"/> (ochiq) bo'ladi, faqat <see cref="Close"/>
    /// uni yopadi. "Rejalashtirilgan" ko'rinishi sanadan hisoblanadi — <see cref="EffectiveStatus"/>.</summary>
    public bool IsOpen => Status != PracticePeriodStatus.Closed;

    /// <summary>Foydalanuvchiga ko'rinadigan holat: yopilgan → <c>Closed</c>; boshlanish sanasi bugundan keyin →
    /// <c>Planned</c>; aks holda <c>Active</c>.</summary>
    public PracticePeriodStatus EffectiveStatus(DateOnly today) => ResolveStatus(Status, StartDate, today);

    /// <summary><see cref="EffectiveStatus"/> ning statik shakli — so'rov proyeksiyalari uchun.</summary>
    public static PracticePeriodStatus ResolveStatus(PracticePeriodStatus stored, DateOnly startDate, DateOnly today)
        => stored == PracticePeriodStatus.Closed
            ? PracticePeriodStatus.Closed
            : startDate > today ? PracticePeriodStatus.Planned : PracticePeriodStatus.Active;

    /// <summary>[start, end] oralig'idagi ish kunlari soni (hafta kuni bo'yicha, bayramlarsiz) — <see cref="RequiredDays"/>.</summary>
    public static int CountWorkDays(DateOnly startDate, DateOnly endDate, WorkDays workDays, Func<DateOnly, bool> isHoliday)
    {
        ArgumentNullException.ThrowIfNull(isHoliday);
        var count = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (workDays.Includes(date) && !isHoliday(date))
                count++;
        }

        return count;
    }

    public PracticePeriodGroup AttachGroup(Guid studentGroupId)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrga guruh biriktirib bo'lmaydi.");
        if (IncludesGroup(studentGroupId))
            throw new ConflictException("Bu guruh davrga allaqachon biriktirilgan.");

        var link = PracticePeriodGroup.Create(Id, studentGroupId);
        _groups.Add(link);
        return link;
    }

    /// <summary>Guruhni davrdan ajratish. Shu guruh talabalarining shu davrda davomat yozuvi bo'lsa — ajratib bo'lmaydi
    /// (tarix davrga bog'liq). Davomat bor-yo'qligini handler bazadan aniqlaydi.</summary>
    public void DetachGroup(Guid studentGroupId, bool hasAttendanceRecords)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrdan guruhni ajratib bo'lmaydi.");
        if (hasAttendanceRecords)
            throw new ConflictException("Guruh talabalarining shu davrda davomat yozuvlari bor — guruhni ajratib bo'lmaydi.");

        _groups.RemoveAll(g => g.StudentGroupId == studentGroupId);
    }

    public void Activate()
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni qayta faollashtirib bo'lmaydi.");
        Status = PracticePeriodStatus.Active;
    }

    public void Close()
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Davr allaqachon yopilgan.");
        Status = PracticePeriodStatus.Closed;
    }

    public void Extend(DateOnly newEndDate)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni uzaytirib bo'lmaydi.");
        if (newEndDate <= EndDate)
            throw new DomainException("Yangi tugash sanasi joriy sanadan keyin bo'lishi kerak.");
        EndDate = newEndDate;
    }

    /// <summary>Sanalarni o'zgartirish. Yopilgan davr → 409. Boshlangan (faol) davrda boshlanish sanasi o'zgarmaydi,
    /// tugash sanasi esa uzaytiriladi/qisqartiriladi, lekin bugundan oldinga emas. Rejalashtirilgan davrda ikkala sana
    /// ham erkin (faqat <c>end &gt;= start</c>). <paramref name="requiredDays"/> — yangi oraliq bo'yicha qayta hisoblangan.</summary>
    public void Reschedule(DateOnly startDate, DateOnly endDate, int requiredDays, DateOnly today)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni tahrirlab bo'lmaydi.");
        if (endDate < startDate)
            throw new DomainException("Tugash sanasi boshlanish sanasidan oldin bo'lishi mumkin emas.");
        if (requiredDays < 0)
            throw new DomainException("Talab qilinadigan kunlar soni manfiy bo'lishi mumkin emas.");

        if (EffectiveStatus(today) == PracticePeriodStatus.Active)
        {
            if (startDate != StartDate)
                throw new DomainException("Faol davrning boshlanish sanasini o'zgartirib bo'lmaydi.");
            if (endDate != EndDate && endDate < today)
                throw new DomainException("Faol davrning tugash sanasi bugundan oldin bo'lishi mumkin emas.");
        }

        StartDate = startDate;
        EndDate = endDate;
        RequiredDays = requiredDays;
    }

    /// <summary>Kunlik ish vaqti va ish kunlarini o'zgartirish. Yopilgan davr → 409. Davrning kechikish/check-in oynasi/
    /// avto-yopish daqiqalari saqlanadi — yangi soatlar ular bilan birga <see cref="CheckInRules"/> orqali tekshiriladi
    /// (tugash boshlanishdan keyin, check-in oynasi ish tugashigacha yopiladi). <paramref name="requiredDays"/> — yangi
    /// ish kunlari bo'yicha qayta hisoblangan. Davomat yozuvlari o'zgarmaydi — ish kuni belgisi davrdan dinamik o'qiladi.</summary>
    public void ChangeSchedule(TimeOnly dailyStart, TimeOnly dailyEnd, WorkDays workDays, int requiredDays)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni tahrirlab bo'lmaydi.");
        if (workDays == WorkDays.None)
            throw new DomainException("Kamida bitta ish kuni belgilanishi kerak.");
        if (requiredDays < 0)
            throw new DomainException("Talab qilinadigan kunlar soni manfiy bo'lishi mumkin emas.");

        // GPS aniqligi davrga tegishli emas — tekshiruv uchun standart qiymat.
        var rules = new CheckInRules(
            dailyStart, dailyEnd, LateToleranceMinutes, CheckInWindowMinutes, CheckoutGraceMinutes,
            CheckInRules.Default.MinAccuracyM);

        DailyStart = rules.DailyStart;
        DailyEnd = rules.DailyEnd;
        WorkDays = workDays;
        RequiredDays = requiredDays;
    }

    public void Rename(string name)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni tahrirlab bo'lmaydi.");
        Name = NormalizeName(name);
    }

    /// <summary>Soft delete. Davrda davomat yozuvi bo'lsa — o'chirib bo'lmaydi, uni yopish kerak.</summary>
    public void Delete(DateTimeOffset now, bool hasAttendanceRecords)
    {
        if (hasAttendanceRecords)
            throw new ConflictException("Davrda davomat yozuvlari bor — uni o'chirib bo'lmaydi, \"Yopish\" dan foydalaning.");
        IsDeleted = true;
        DeletedAt = now;
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Davr nomi bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"Davr nomi {NameMaxLength} belgidan oshmasligi kerak.");
        return trimmed;
    }
}

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
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Davr nomi bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"Davr nomi {NameMaxLength} belgidan oshmasligi kerak.");
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

    public void DetachGroup(Guid studentGroupId)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrdan guruhni ajratib bo'lmaydi.");

        _groups.RemoveAll(g => g.StudentGroupId == studentGroupId);
    }

    public void Activate()
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni qayta faollashtirib bo'lmaydi.");
        Status = PracticePeriodStatus.Active;
    }

    public void Close() => Status = PracticePeriodStatus.Closed;

    public void Extend(DateOnly newEndDate)
    {
        if (Status == PracticePeriodStatus.Closed)
            throw new ConflictException("Yopilgan davrni uzaytirib bo'lmaydi.");
        if (newEndDate <= EndDate)
            throw new DomainException("Yangi tugash sanasi joriy sanadan keyin bo'lishi kerak.");
        EndDate = newEndDate;
    }

    public void Rename(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Davr nomi bo'sh bo'lishi mumkin emas.");
        Name = trimmed;
    }
}

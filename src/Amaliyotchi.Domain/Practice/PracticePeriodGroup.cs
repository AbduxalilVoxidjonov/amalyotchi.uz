using Amaliyotchi.Domain.Common;

namespace Amaliyotchi.Domain.Practice;

/// <summary>Amaliyot davri ↔ guruh bog'lovchisi. Talabaning faol davri = guruhi biriktirilgan faol davr.</summary>
public sealed class PracticePeriodGroup : BaseEntity
{
    private PracticePeriodGroup() { }

    public Guid PeriodId { get; private set; }
    public Guid StudentGroupId { get; private set; }

    internal static PracticePeriodGroup Create(Guid periodId, Guid studentGroupId)
        => new() { PeriodId = periodId, StudentGroupId = studentGroupId };
}

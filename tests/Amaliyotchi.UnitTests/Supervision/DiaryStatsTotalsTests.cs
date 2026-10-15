using Amaliyotchi.Application.Features.Supervision.Common;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Supervision;

/// <summary>Baholash jadvali kundalik statistikasini SQL agregatlaridan quradi (<see cref="StudentStatsCalculator.DiaryFromTotals"/>) —
/// natija har yozuvni xotirada sanagan <see cref="StudentStatsCalculator.ComputeDiary"/> bilan aynan bir xil bo'lishi shart.</summary>
public sealed class DiaryStatsTotalsTests
{
    public static TheoryData<int?[]> Cases => new()
    {
        Array.Empty<int?>(),
        new int?[] { null, null },
        new int?[] { 5 },
        new int?[] { 4, 5, null },
        new int?[] { 3, 4, 4 },          // 3.666… → 3.7
        new int?[] { 1, 2, 2, 2, null }, // 1.75 → 1.8 (AwayFromZero)
        new int?[] { 2, 3 },             // 2.5
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void DiaryFromTotals_ComputeDiary_BilanBirXil(int?[] scores)
    {
        var scored = scores.Where(s => s is not null).Select(s => s!.Value).ToList();

        var fromTotals = StudentStatsCalculator.DiaryFromTotals(scores.Length, scored.Count, scored.Sum());

        fromTotals.Should().Be(StudentStatsCalculator.ComputeDiary(scores));
    }
}

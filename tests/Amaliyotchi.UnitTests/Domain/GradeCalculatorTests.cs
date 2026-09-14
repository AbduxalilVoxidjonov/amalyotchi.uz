using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Grading;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class GradeCalculatorTests
{
    [Fact]
    public void QollanmadagiMisol_5Baho()
    {
        // Davomat 94% → 37,6; hisobot o'rtacha 4,2 → 25,2; tyutor 18; tavsifnoma 10 → 90,8 → 5
        var result = GradeCalculator.Compute(94.44, diaryCount: 32, diaryAvg: 4.2, tutorPoints: 18, referencePoints: 10);

        result.AttendancePoints.Should().Be(37.8);
        result.ReportPoints.Should().Be(25.2);
        result.Total.Should().Be(91.0);
        result.Grade.Should().Be(5);
        result.MustRetake.Should().BeFalse();
    }

    [Theory]
    [InlineData(100, 5.0, 20, 10, 100.0, 5)]
    [InlineData(80, 4.0, 14, 7, 77.0, 4)]
    [InlineData(75, 3.0, 10, 5, 63.0, 3)]
    [InlineData(70, 2.0, 5, 2, 47.0, 2)]
    public void Chegaralar_BahogaAylanadi(double pct, double avg, int tutor, int reference, double total, int grade)
    {
        var result = GradeCalculator.Compute(pct, 10, avg, tutor, reference);

        result.Total.Should().Be(total);
        result.Grade.Should().Be(grade);
    }

    [Fact]
    public void Davomat70danPast_BahoQoyilmaydi()
    {
        var result = GradeCalculator.Compute(69.9, 30, 5.0, 20, 10);

        result.Grade.Should().BeNull();
        result.MustRetake.Should().BeTrue();
        result.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public void HisobotYoq_HisobotBaliNol()
    {
        var result = GradeCalculator.Compute(90, diaryCount: 0, diaryAvg: 0, tutorPoints: null, referencePoints: null);

        result.ReportPoints.Should().Be(0);
        result.TutorPoints.Should().Be(0);
        result.ReferencePoints.Should().Be(0);
        result.Total.Should().Be(36.0);
        result.Grade.Should().Be(2);
        result.RecommendedTutorPoints.Should().Be(0);
        result.RecommendedReferencePoints.Should().Be(9);
    }

    [Fact]
    public void TavsiyaEtilganBallar_OrtachaVaFoizdan()
    {
        var result = GradeCalculator.Compute(94, 20, 4.5, null, null);

        result.RecommendedTutorPoints.Should().Be(18);
        result.RecommendedReferencePoints.Should().Be(9);
    }

    [Theory]
    [InlineData(101, 5.0, 10, 5)]
    [InlineData(90, 6.0, 10, 5)]
    [InlineData(90, 4.0, 21, 5)]
    [InlineData(90, 4.0, 10, 11)]
    public void NotogriKirish_XatoBeradi(double pct, double avg, int tutor, int reference)
    {
        var act = () => GradeCalculator.Compute(pct, 10, avg, tutor, reference);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PracticeGrade_YakunlangachOzgartirilmaydi_RevertOchadi()
    {
        var grade = PracticeGrade.Create(Guid.CreateVersion7(), Guid.CreateVersion7());
        grade.SetTutorPoints(18);
        grade.SetReferencePoints(9);
        grade.Finalize("Talaba a'lo darajada o'tdi.", Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var act = () => grade.SetTutorPoints(10);
        act.Should().Throw<ConflictException>();

        grade.Revert();
        grade.SetTutorPoints(10);
        grade.TutorPoints.Should().Be(10);
    }

    [Fact]
    public void PracticeGrade_TyutorBalisizYakunlanmaydi()
    {
        var grade = PracticeGrade.Create(Guid.CreateVersion7(), Guid.CreateVersion7());

        var act = () => grade.Finalize("Xulosa", Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>();
    }
}

using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class PracticeApplicationTests
{
    private static readonly Guid Tutor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static PracticeApplication Submitted()
        => PracticeApplication.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 200, null, Now);

    [Fact]
    public void Create_YuborilganHolatda()
    {
        var app = Submitted();

        app.Status.Should().Be(ApplicationStatus.Submitted);
        app.IsDecided.Should().BeFalse();
        app.RevisionCount.Should().Be(0);
    }

    [Fact]
    public void Approve_RadiusVaChecklistSaqlanadi()
    {
        var app = Submitted();

        app.Approve(Tutor, 150, [3, 0, 3, 6], "OK", Now);

        app.Status.Should().Be(ApplicationStatus.Approved);
        app.ProposedRadiusM.Should().Be(150);
        app.Checklist.Should().Equal(0, 3, 6);
        app.DecidedByUserId.Should().Be(Tutor);
        app.DecisionComment.Should().Be("OK");
    }

    [Fact]
    public void Approve_NotogriChecklistIndeksi_XatoBeradi()
    {
        var act = () => Submitted().Approve(Tutor, 200, [7], null, Now);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(49)]
    [InlineData(1001)]
    public void Approve_RadiusOraliqdanTashqari_XatoBeradi(int radius)
    {
        var act = () => Submitted().Approve(Tutor, radius, [], null, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void HalQilinganAriza_QaytaHalQilinmaydi()
    {
        var app = Submitted();
        app.Approve(Tutor, 200, [], null, Now);

        var approve = () => app.Approve(Tutor, 200, [], null, Now);
        var reject = () => app.Reject(Tutor, "sabab", Now);
        var back = () => app.ReturnForRevision(Tutor, "sabab", Now);

        approve.Should().Throw<ConflictException>();
        reject.Should().Throw<ConflictException>();
        back.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Reject_IzohsizBolmaydi()
    {
        var act = () => Submitted().Reject(Tutor, " ", Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReturnForRevision_KeyinResubmit_YanaYuborilgan()
    {
        var app = Submitted();
        var newCompany = Guid.CreateVersion7();

        app.ReturnForRevision(Tutor, "Shartnoma imzosiz", Now);
        app.Status.Should().Be(ApplicationStatus.RevisionNeeded);
        app.RevisionCount.Should().Be(1);

        app.Resubmit(newCompany, 300, Guid.CreateVersion7(), Now.AddHours(1));

        app.Status.Should().Be(ApplicationStatus.Submitted);
        app.CompanyId.Should().Be(newCompany);
        app.DecidedAt.Should().BeNull();
        app.DecisionComment.Should().BeNull();
    }

    [Fact]
    public void Resubmit_FaqatQaytarilganHolatdan()
    {
        var act = () => Submitted().Resubmit(Guid.CreateVersion7(), 200, null, Now);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Complete_FaqatTasdiqlanganArizada()
    {
        var app = Submitted();
        var early = () => app.Complete();
        early.Should().Throw<ConflictException>();

        app.Approve(Tutor, 200, [], null, Now);
        app.Complete();

        app.Status.Should().Be(ApplicationStatus.Completed);
    }
}

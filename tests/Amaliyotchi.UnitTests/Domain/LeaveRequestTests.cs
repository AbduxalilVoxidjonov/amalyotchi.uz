using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Leave;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

public sealed class LeaveRequestTests
{
    private static readonly DateOnly From = new(2026, 10, 5);

    private static LeaveRequest Pending(DateOnly? to = null)
        => LeaveRequest.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), From, to ?? From, "Kasallik varaqasi bor");

    [Fact]
    public void BirKunlik_DateToTeng_KunSoniBir()
    {
        var request = Pending();

        request.DateTo.Should().Be(From);
        request.DayCount.Should().Be(1);
        request.Status.Should().Be(LeaveRequestStatus.Pending);
        request.Covers(From).Should().BeTrue();
        request.Covers(From.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void TugashSanasiOldin_XatoBeradi()
    {
        var act = () => Pending(From.AddDays(-1));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QisqaSabab_XatoBeradi()
    {
        var act = () => LeaveRequest.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), From, From, "qisqa");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_KeyinReject_ZiddiyatBeradi()
    {
        var request = Pending();
        var tutor = Guid.CreateVersion7();

        request.Approve(tutor, null, DateTimeOffset.UtcNow);
        var act = () => request.Reject(tutor, "yo'q", DateTimeOffset.UtcNow);

        request.Status.Should().Be(LeaveRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(tutor);
        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Reject_IzohSaqlanadi()
    {
        var request = Pending();

        request.Reject(Guid.CreateVersion7(), "  Hujjat yo'q  ", DateTimeOffset.UtcNow);

        request.Status.Should().Be(LeaveRequestStatus.Rejected);
        request.DecisionComment.Should().Be("Hujjat yo'q");
    }
}

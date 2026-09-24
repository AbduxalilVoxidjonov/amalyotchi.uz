using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Amaliyotchi.UnitTests.Domain;

/// <summary>Check-in selfisi: hodisa va kunlik qatordagi rasm havolalari, <c>checkinPhotoRequired</c> sozlamasi.</summary>
public sealed class AttendancePhotoTests
{
    private static readonly DateOnly Monday = new(2026, 9, 14);
    private static readonly GeoPoint Point = new(41.3111, 69.2797);

    [Fact]
    public void StoredFileKind_CheckInPhoto_BeshinchiQiymat()
    {
        ((int)StoredFileKind.CheckInPhoto).Should().Be(5);
    }

    [Fact]
    public void Hodisa_RasmsizHam_RasmBilanHam_Yoziladi()
    {
        var photoId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var withPhoto = AttendanceEvent.Record(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, AttendanceEventKind.CheckIn, now, now,
            Point, 10, 20, 150, CheckInVerdict.Accept(), photoId);
        var withoutPhoto = AttendanceEvent.Record(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, AttendanceEventKind.CheckIn, now, now,
            Point, 10, 20, 150, CheckInVerdict.Accept());

        withPhoto.PhotoFileId.Should().Be(photoId);
        withoutPhoto.PhotoFileId.Should().BeNull();
    }

    [Fact]
    public void RadEtilganUrinish_RasmniSaqlaydi()
    {
        var photoId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var rejected = AttendanceEvent.Record(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, AttendanceEventKind.CheckIn, now, now,
            Point, 10, 900, 150, CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius), photoId);

        rejected.Accepted.Should().BeFalse();
        rejected.PhotoFileId.Should().Be(photoId, "tyutor shubhani rasm bo'yicha tekshiradi");
    }

    [Fact]
    public void KunlikQator_CheckInVaCheckOutRasmlari()
    {
        var checkInPhoto = Guid.CreateVersion7();
        var checkOutPhoto = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var attendance = DailyAttendance.CheckIn(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Monday, now, 45, 20, CheckInVerdict.Accept(), checkInPhoto);

        attendance.CheckInPhotoFileId.Should().Be(checkInPhoto);
        attendance.CheckOutPhotoFileId.Should().BeNull();

        attendance.CheckOut(now.AddHours(8), 30, checkOutPhoto);
        attendance.CheckOutPhotoFileId.Should().Be(checkOutPhoto);
    }

    [Fact]
    public void Sozlama_CheckInPhotoRequired_MantiqiyVaStandartYoqilgan()
    {
        var definition = SettingKeys.Get(SettingKeys.CheckInPhotoRequired);

        SettingKeys.CheckInPhotoRequired.Should().Be("checkinPhotoRequired");
        definition.Type.Should().Be(SettingType.Bool);
        definition.DefaultValue.Should().Be("true");
        definition.Validate("ha").Should().Be("true");
        definition.Validate("yo'q").Should().Be("false");
    }
}

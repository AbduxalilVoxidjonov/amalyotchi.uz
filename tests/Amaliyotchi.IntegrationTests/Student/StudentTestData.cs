using System.Globalization;
using System.Net.Http.Headers;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Talaba testlari uchun tayyor sahna: guruh → tyutor → talaba → korxona → davr → tasdiqlangan ariza → kirgan klient.</summary>
public sealed record StudentScene(
    TestGroup Group,
    TestUser Tutor,
    TestUser Student,
    Company Company,
    PracticePeriod Period,
    PracticeApplication? Application,
    HttpClient Client);

/// <summary>Vaqtga bog'liq testlar: davr standart qoidalar (09:00–17:00, 15 daqiqa kechikish, 90 daqiqa oyna,
/// 60 daqiqa avto-yopish) bilan <see cref="NextWorkDayAsync"/> kuni atrofida yaratiladi, soat esa
/// <see cref="MutableClock"/> bilan kerakli momentga muzlatiladi (<c>fixture.Clock.Set(PracticeTime.At(day, 09:05))</c>,
/// <c>finally { Reset(); }</c>) — haqiqiy kun vaqtiga bog'liq emas. Token muzlatishdan OLDIN olinadi
/// (<see cref="CreateSceneAsync"/> oxirida) — JwtBearer o'z soati bilan tekshiradi.</summary>
public static class StudentTestData
{
    /// <summary>Korxona nuqtasi (Amir Temur 108).</summary>
    public const double CompanyLat = 41.3111;
    public const double CompanyLng = 69.2797;

    /// <summary>Korxonadan ~1 km shimolda — har qanday radiusdan tashqarida.</summary>
    public const double FarLat = 41.3201;

    public static DateOnly LocalToday(this ApiFactory factory)
        => PracticeTime.LocalDate(factory.Services.GetRequiredService<IClock>().UtcNow);

    /// <summary>Bugun yoki undan keyingi birinchi bayram bo'lmagan kun (har hafta kuni ish kuni bo'lgan davr uchun) —
    /// <see cref="MutableClock"/> bilan soat shu kunga qo'yiladi.</summary>
    public static Task<DateOnly> NextWorkDayAsync(this ApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            var holidays = await db.Holidays.AsNoTracking().ToListAsync();
            var day = factory.LocalToday();
            while (holidays.Any(h => h.AppliesTo(day)))
                day = day.AddDays(1);
            return day;
        });

    /// <summary>Faol davr (<paramref name="day"/> −14..+30 kun), qoidalar <paramref name="rules"/> (standart:
    /// 09:00–17:00, 15/90/60 daqiqa), ish kunlari standart — har kuni (bayramlar bundan mustasno).</summary>
    public static Task<PracticePeriod> CreatePeriodAtAsync(
        this ApiFactory factory,
        TestGroup group,
        Guid createdByUserId,
        DateOnly day,
        CheckInRules? rules = null,
        WorkDays? workDays = null,
        int startDaysAgo = 14,
        int endDaysAhead = 30) =>
        factory.WithDbAsync(async db =>
        {
            var period = PracticePeriod.Create(
                $"Amaliyot {Guid.NewGuid():N}"[..20], group.AcademicYearId, day.AddDays(-startDaysAgo), day.AddDays(endDaysAhead),
                createdByUserId, rules ?? CheckInRules.Default, workDays ?? WorkDays.MondayToSaturday | WorkDays.Sunday, 36,
                dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    /// <summary>To'liq sahna. Davr <paramref name="day"/> (berilmasa — API soati bo'yicha bugun) atrofida standart qoidalar bilan
    /// (<see cref="CreatePeriodAtAsync"/>); <paramref name="period"/> berilsa u ishlatiladi. <paramref name="approve"/> false bo'lsa
    /// ariza yaratilmaydi. Talaba tokeni shu yerda (soat muzlatilishidan oldin) olinadi.</summary>
    public static async Task<StudentScene> CreateSceneAsync(
        this ApiFactory factory,
        DateOnly? day = null,
        Func<TestGroup, Guid, Task<PracticePeriod>>? period = null,
        bool approve = true,
        int radiusM = 150)
    {
        var group = await factory.CreateGroupAsync();
        var tutor = await factory.CreateTutorAsync(group);
        var student = await factory.CreateStudentAsync(group: group);
        var company = await factory.CreateCompanyAsync(CompanyLat, CompanyLng, radiusM);
        var practicePeriod = period is null
            ? await factory.CreatePeriodAtAsync(group, tutor.Id, day ?? factory.LocalToday())
            : await period(group, tutor.Id);

        PracticeApplication? application = null;
        if (approve)
            application = await factory.CreateApprovedApplicationAsync(student, practicePeriod, company, tutor.Id);

        var client = await factory.LoginAsStudentAsync(student);
        return new StudentScene(group, tutor, student, company, practicePeriod, application, client);
    }

    /// <summary>Kontrakt <c>CheckinRequest</c>: API soati (muzlatilgan bo'lsa — o'sha moment) bo'yicha 2 soniya oldingi urinish
    /// (validator <c>occurredAt</c> ni <c>IClock</c> bilan solishtiradi), korxona nuqtasi (yoki berilgan), aniqlik 10 m.</summary>
    public static object Geo(this ApiFactory factory, double lat = CompanyLat, double lng = CompanyLng, double accuracy = 10)
        => Geo(factory.Clock.UtcNow.AddSeconds(-2), lat, lng, accuracy);

    /// <summary>Kontrakt <c>CheckinRequest</c> aniq <paramref name="occurredAt"/> bilan.</summary>
    public static object Geo(DateTimeOffset occurredAt, double lat = CompanyLat, double lng = CompanyLng, double accuracy = 10)
        => new { lat, lng, accuracy, occurredAt };

    /// <summary><paramref name="day"/> uchun davomat qatorini to'g'ridan-to'g'ri yozadi (check-in oynasi yopiq holatlar uchun),
    /// kelish vaqti <paramref name="at"/> (standart 09:00).</summary>
    public static Task<DailyAttendance> CheckInDirectlyAsync(
        this ApiFactory factory, StudentScene scene, DateOnly day, bool late = false, TimeOnly? at = null) =>
        factory.WithDbAsync(async db =>
        {
            var when = PracticeTime.At(day, at ?? new TimeOnly(9, 0));
            var attendance = DailyAttendance.CheckIn(
                scene.Student.Id, scene.Period.Id, day, when, 20, 10, CheckInVerdict.Accept(late));
            db.DailyAttendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance;
        });

    /// <summary>Check-in/check-out multipart formasi: <c>lat</c>, <c>lng</c>, <c>accuracy</c>, <c>occurredAt</c>
    /// va ixtiyoriy <c>photo</c>. <c>occurredAt</c> berilmasa — API soati bo'yicha 2 soniya oldin.</summary>
    public static MultipartFormDataContent GeoForm(
        this ApiFactory factory,
        double lat = CompanyLat,
        double lng = CompanyLng,
        double accuracy = 10,
        DateTimeOffset? occurredAt = null,
        (string Name, string ContentType, byte[] Bytes)? photo = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(lat.ToString(CultureInfo.InvariantCulture)), "lat" },
            { new StringContent(lng.ToString(CultureInfo.InvariantCulture)), "lng" },
            { new StringContent(accuracy.ToString(CultureInfo.InvariantCulture)), "accuracy" },
            { new StringContent((occurredAt ?? factory.Clock.UtcNow.AddSeconds(-2)).ToString("O", CultureInfo.InvariantCulture)), "occurredAt" }
        };

        if (photo is var (name, contentType, bytes))
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(part, "photo", name);
        }

        return form;
    }

    /// <summary>Global sozlamani vaqtincha o'zgartiradi; <c>Dispose</c> da avvalgi qiymat qaytariladi
    /// (integratsiya testlari bitta kolleksiyada ketma-ket ishlaydi).</summary>
    public static async Task<IAsyncDisposable> UseSettingAsync(this ApiFactory factory, string key, string value)
    {
        var previous = await factory.WithDbAsync(async db =>
        {
            var setting = await db.AppSettings.SingleAsync(s => s.Key == key);
            var old = setting.Value;
            setting.Update(value, DateTimeOffset.UtcNow, null);
            await db.SaveChangesAsync();
            return old;
        });

        return new SettingReset(factory, key, previous);
    }

    private sealed class SettingReset(ApiFactory factory, string key, string previous) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            await factory.WithDbAsync(async db =>
            {
                var setting = await db.AppSettings.SingleAsync(s => s.Key == key);
                setting.Update(previous, DateTimeOffset.UtcNow, null);
                await db.SaveChangesAsync();
            });
    }

    public static MultipartFormDataContent DiaryForm(string text, string? learned = null, params (string Name, string ContentType, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(text), "text");
        if (learned is not null)
            form.Add(new StringContent(learned), "learned");
        foreach (var (name, contentType, bytes) in files)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(part, "files", name);
        }

        return form;
    }

    public static string LongText(int length = 200) => new('a', length);
}

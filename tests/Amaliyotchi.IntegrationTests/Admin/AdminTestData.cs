using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Admin testlari uchun qo'shimcha ma'lumot yaratuvchilar (umumiy <c>TestClients</c> ga tegilmaydi).</summary>
internal static class AdminTestData
{
    public static DateOnly Today(this ApiFactory factory)
        => PracticeTime.LocalDate(factory.Services.GetRequiredService<IClock>().UtcNow);

    public static DateTimeOffset Now(this ApiFactory factory)
        => factory.Services.GetRequiredService<IClock>().UtcNow;

    /// <summary>Berilgan kunga davomat yozuvi (keldi / kech keldi), ixtiyoriy shubhali belgisi.</summary>
    public static Task<DailyAttendance> CheckInAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, DateOnly date,
        bool late = false, bool suspicious = false, double distanceM = 20)
        => factory.WithDbAsync(async db =>
        {
            var at = PracticeTime.At(date, new TimeOnly(late ? 9 : 8, late ? 30 : 55));
            var attendance = DailyAttendance.CheckIn(student.Id, period.Id, date, at, distanceM, 10, CheckInVerdict.Accept(late));
            if (suspicious)
                attendance.MarkSuspicious("Radius chetida");
            db.DailyAttendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance;
        });

    /// <summary>Kutilayotgan (Submitted) ariza; <paramref name="submittedAgo"/> — qancha oldin yuborilgan.</summary>
    public static Task<PracticeApplication> CreatePendingApplicationAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, Domain.Companies.Company company, TimeSpan submittedAgo)
        => factory.WithDbAsync(async db =>
        {
            var application = PracticeApplication.Create(
                student.Id, period.Id, company.Id, company.RadiusM, null, factory.Now() - submittedAgo);
            db.PracticeApplications.Add(application);
            await db.SaveChangesAsync();
            return application;
        });

    /// <summary>Telegram hisobi bog'lanmagan talaba (<c>unlinked</c> holati uchun).</summary>
    public static Task<TestUser> CreateUnlinkedStudentAsync(this ApiFactory factory, TestGroup group, string? fullName = null)
        => factory.WithDbAsync(async db =>
        {
            var user = Domain.Identity.User.CreateStudent(fullName ?? "Ulanmagan Talaba", group.FacultyId);
            var profile = Domain.Students.StudentProfile.Create(user.Id, TestClients.RandomHemisId(), group.GroupId);
            db.Users.Add(user);
            db.StudentProfiles.Add(profile);
            await db.SaveChangesAsync();
            return new TestUser(user.Id, user.FullName, user.Role, string.Empty, string.Empty, group.FacultyId, null, group.GroupId);
        });
}

internal static class AdminJson
{
    public static Task<T?> GetFromJsonAsync<T>(this HttpClient client, string url)
        => System.Net.Http.Json.HttpClientJsonExtensions.GetFromJsonAsync<T>(client, url, JsonDefaults.Options);

    public static Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpClient client, string url, T body)
        => System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(client, url, body, JsonDefaults.Options);
}

using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Application qatlami DbContext'ni to'g'ridan-to'g'ri emas, shu interfeys orqali ko'radi.
/// Har bir entity uchun alohida repository yozilmaydi — DbSet allaqachon repository.</summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AcademicYear> AcademicYears { get; }
    DbSet<Faculty> Faculties { get; }
    DbSet<Department> Departments { get; }
    DbSet<Direction> Directions { get; }
    DbSet<StudentGroup> StudentGroups { get; }
    DbSet<StudentProfile> StudentProfiles { get; }
    DbSet<TutorAssignment> TutorAssignments { get; }
    DbSet<TutorScope> TutorScopes { get; }
    DbSet<TutorFaculty> TutorFaculties { get; }
    DbSet<Company> Companies { get; }
    DbSet<PracticePeriod> PracticePeriods { get; }
    DbSet<PracticePeriodGroup> PracticePeriodGroups { get; }
    DbSet<PracticeApplication> PracticeApplications { get; }
    DbSet<DailyAttendance> DailyAttendances { get; }
    DbSet<AttendanceEvent> AttendanceEvents { get; }
    DbSet<DiaryEntry> DiaryEntries { get; }
    DbSet<DiaryAttachment> DiaryAttachments { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<PracticeGrade> PracticeGrades { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<DocumentTemplate> DocumentTemplates { get; }
    DbSet<StoredFile> StoredFiles { get; }

    /// <summary>Tranzaksiya va execution strategy (<c>CreateExecutionStrategy</c> / <c>BeginTransactionAsync</c>) uchun.
    /// Npgsql retry yoqilgan — foydalanuvchi tranzaksiyasi doimo strategy ichida ochilishi kerak
    /// (<c>DbTransactions.InTransactionAsync</c>).</summary>
    DatabaseFacade Database { get; }

    /// <summary>Joriy tranzaksiya tugaguncha <paramref name="key"/> bo'yicha eksklyuziv qulf (Postgres
    /// <c>pg_advisory_xact_lock</c>). Faqat ochiq tranzaksiya ichida (<c>DbTransactions.InTransactionAsync</c>) chaqiriladi —
    /// bir xil kalit bilan parallel so'rovlar navbatga turadi (masalan, bir loginni ikki so'rov egallamasligi uchun).</summary>
    Task AcquireTransactionLockAsync(string key, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

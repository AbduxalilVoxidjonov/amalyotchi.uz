using System.Data;
using System.Globalization;
using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Infrastructure.Persistence.Seeding;

/// <summary>Bitta jadval bo'yicha o'chiriladigan (dry-run: o'chirilishi kerak bo'lgan) qatorlar soni.</summary>
public sealed record DemoPurgeTableCount(string Table, int Count);

/// <summary><see cref="DemoDataPurger"/> natijasi. <see cref="Found"/> = false — bazada demo ma'lumot yo'q (idempotent holat).
/// <see cref="Blockers"/> bo'sh bo'lmasa hech narsa o'chirilmaydi (<see cref="Applied"/> = false).</summary>
public sealed class DemoPurgeReport
{
    public bool Found { get; init; }
    public string? Anchor { get; init; }
    public IReadOnlyList<DemoPurgeTableCount> Counts { get; init; } = [];
    public IReadOnlyList<string> Blockers { get; init; } = [];
    public bool Applied { get; init; }

    /// <summary>Diskdan (IFileStorage) o'chirilgan fayllar — faqat tranzaksiya commit bo'lgandan keyin.</summary>
    public int FilesDeleted { get; init; }

    public IReadOnlyList<string> FileErrors { get; init; } = [];

    public int TotalRows => Counts.Sum(c => c.Count);
    public int Count(string table) => Counts.FirstOrDefault(c => c.Table == table)?.Count ?? 0;
}

/// <summary>
/// <see cref="DemoDataSeeder"/> yaratgan demo ma'lumotni xavfsiz o'chiradi (CLI: <c>dotnet Amaliyotchi.Api.dll purge-demo [--apply]</c>).
/// <para><b>Identifikatsiya</b> — nom bo'yicha emas, birgalikdagi belgilar bo'yicha (haqiqiy universitetda ham "AT" fakulteti
/// yoki "412-22" guruhi bo'lishi mumkin):</para>
/// <list type="number">
/// <item>Langar — demo tyutor: rol Tutor + telefon <see cref="DemoDataSeeder.TutorPhone"/> + FISH "Nodira Saidova" + HEMIS ID
/// <see cref="DemoDataSeeder.TutorHemisId"/> (eng eski seed'da null) + <c>CreatedBy IS NULL</c> (seed HTTP so'rovsiz — audit
/// interceptor foydalanuvchini yozmaydi; admin yaratgan yozuvda doim admin Id). Langar yo'q → "demo ma'lumot topilmadi".</item>
/// <item>Seed bir necha <c>SaveChanges</c> bilan yozadi, interceptor bitta <c>SaveChanges</c> dagi barcha yozuvlarga AYNAN bir xil
/// <c>CreatedAt</c> qo'yadi. 1-paket (fakultet→kafedra→yo'nalish→guruh) vaqti = langar fakultetining <c>CreatedAt</c>;
/// 2-paket (tyutorlar, korxonalar, talabalar) vaqti = langar tyutorning <c>CreatedAt</c>. Har demo yozuv: seed'dagi aniq
/// (nom, kod / STIR / HEMIS ID) jufti + o'z paketining vaqt belgisi + <c>CreatedBy IS NULL</c>.</item>
/// <item>Davrlar — demo nomli, <c>CreatedBy IS NULL</c> va faqat demo guruhlarga biriktirilgan.</item>
/// <item>Talabaga tegishli qatorlar (ariza, davomat, hodisa, kundalik, ruxsat, baho) — demo talabaniki; fayllar — demo
/// foydalanuvchi yuklagan yoki faqat demo qatorlar ishlatadigan; refresh token'lar va audit yozuvlari — demo foydalanuvchi
/// yoki o'chirilayotgan yozuv Id'siga tegishli.</item>
/// </list>
/// <para><b>To'siqlar</b> (hech narsa o'chirilmaydi): demo ierarxiyada seed'da bo'lmagan kafedra/yo'nalish/guruh; demo guruhda
/// demo bo'lmagan talaba; demo fakultetga bog'langan boshqa foydalanuvchi/tyutor (asosiy fakultet, tutor_faculties, ko'lam,
/// biriktiruv); demo tyutorning demo bo'lmagan guruh/fakultetga biriktiruvi; demo guruh demo bo'lmagan davrda yoki demo
/// davrda demo bo'lmagan guruh; demo davr/korxonada boshqa talabaning yozuvi; demo talabaning demo bo'lmagan davrdagi yozuvi;
/// demo foydalanuvchi qaror qilgan/tasdiqlagan/yaratgan demo bo'lmagan yozuv; demo foydalanuvchi yuklagan faylning demo
/// bo'lmagan yozuvda ishlatilishi. Shu tufayli kaskad (refresh_tokens, tutor_faculties, diary_attachments,
/// practice_period_groups) haqiqiy ma'lumotga yetib bormaydi; qolgan FK'lar Restrict — kutilmagan bog'lanish bo'lsa
/// DELETE xato beradi va tranzaksiya to'liq qaytariladi.</para>
/// <para>DbSeeder ma'lumoti (admin, sozlamalar, bayramlar, o'quv yili, hujjat shablonlari) hech qachon o'chirilmaydi — demo
/// seed o'quv yilini DbSeeder'nikidan oladi (eng eski versiyada o'zi yaratgan bo'lsa ham u endi asosiy faol yil).</para>
/// <para>Hammasi bitta RepeatableRead tranzaksiyada (+ advisory lock); har DELETE soni rejadagiga teng bo'lmasa — rollback.
/// O'chirish soft-delete interceptor'ini chetlab o'tadi (<c>ExecuteDelete</c> — jismoniy o'chirish). Diskdagi fayllar commit'dan keyin.</para>
/// </summary>
public sealed class DemoDataPurger(
    AppDbContext db,
    IFileStorage storage,
    IClock clock,
    ILogger<DemoDataPurger> logger)
{
    public const string AnchorTutorName = "Nodira Saidova";

    private const string LegacyDepartmentName = "Umumiy kafedra"; // DepartmentsHierarchy migratsiyasi yaratgan
    private const int MaxListed = 15;

    private static readonly (string Name, string Code)[] DemoFaculties =
    [
        ("Axborot texnologiyalari", "AT"), ("Iqtisodiyot va moliya", "IM"),
        ("Qurilish va arxitektura", "QA"), ("Filologiya", "FL")
    ];

    private static readonly (string FacultyCode, string Name, string Code)[] DemoDepartments =
    [
        ("AT", "Dasturiy injiniring kafedrasi", "SE"), ("AT", "Kompyuter injiniringi kafedrasi", "CE"),
        ("IM", "Moliya va bank ishi kafedrasi", "FB"), ("QA", "Qurilish muhandisligi kafedrasi", "CM")
    ];

    private static readonly (string Name, string Code, string Group)[] DemoDirections =
    [
        ("Dasturiy injiniring", "60610500", "412-22"), ("Kompyuter injiniringi", "60610400", "413-22"),
        ("Bank ishi", "60410100", "221-23"), ("Qurilish muhandisligi", "60730100", "318-21")
    ];

    private static readonly (string Name, string? HemisId, string Phone, string FacultyCode)[] DemoTutors =
    [
        (AnchorTutorName, DemoDataSeeder.TutorHemisId, DemoDataSeeder.TutorPhone, "AT"),
        ("Baxtiyor Rasulov", "100000000003", "+998912445102", "IM"),
        ("Dilshod Ergashev", "100000000004", "+998937001845", "QA")
    ];

    private static readonly (string Name, string Tin)[] DemoCompanies =
    [
        ("Tech Solutions MChJ", "304512889"), ("Agrobank ATB", "201344712"), ("Uzinfocom", "203112340"),
        ("Mediapark", "305447120"), ("Qurilish Trest 12", "305881204"), ("Ipak Yuli Bank", "200945187")
    ];

    private static readonly string[] DemoPeriodNames = [DemoDataSeeder.PeriodName, DemoDataSeeder.SpringPeriodName];

    /// <summary>HEMIS ID → FISH: 341030..341035 asosiy, 341040.. qo'shimcha (seed bilan bir manba).</summary>
    private static readonly Dictionary<string, string> DemoStudents = BuildStudentCatalog();

    /// <summary>Dry-run: hech narsa o'zgartirmaydi — nima o'chishini va to'siqlarni qaytaradi.</summary>
    public Task<DemoPurgeReport> AnalyzeAsync(CancellationToken cancellationToken = default)
        => RunAsync(apply: false, cancellationToken);

    /// <summary>To'siq bo'lmasa demo ma'lumotni bitta tranzaksiyada o'chiradi, keyin diskdagi fayllarni.</summary>
    public Task<DemoPurgeReport> PurgeAsync(CancellationToken cancellationToken = default)
        => RunAsync(apply: true, cancellationToken);

    private async Task<DemoPurgeReport> RunAsync(bool apply, CancellationToken cancellationToken)
    {
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            throw new InvalidOperationException(
                "Bazada qo'llanmagan migratsiyalar bor — avval API'ni yangi versiyada ishga tushiring (migratsiya), keyin purge-demo.");

        // NpgsqlRetryingExecutionStrategy foydalanuvchi tranzaksiyasini faqat strategiya ichida qabul qiladi;
        // qayta urinishda reja tranzaksiya ichida qaytadan quriladi.
        var strategy = db.Database.CreateExecutionStrategy();
        var (report, storagePaths) = await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await db.AcquireTransactionLockAsync("purge-demo", ct);

            var plan = await BuildPlanAsync(ct);
            if (!plan.Found || plan.Blockers.Count > 0 || !apply)
            {
                await transaction.RollbackAsync(ct);
                return (plan.ToReport(applied: false), (IReadOnlyList<string>)[]);
            }

            await ExecuteAsync(plan, ct);
            await transaction.CommitAsync(ct);
            return (plan.ToReport(applied: true), (IReadOnlyList<string>)plan.StoragePaths);
        }, cancellationToken);

        if (!report.Applied)
            return report;

        logger.LogWarning("purge-demo: demo ma'lumot o'chirildi — {Rows} qator ({Tables})",
            report.TotalRows, string.Join(", ", report.Counts.Where(c => c.Count > 0).Select(c => $"{c.Table}={c.Count}")));

        // Fayllar faqat commit'dan keyin: rollback bo'lsa disk joyida qoladi. Disk xatosi bazani qaytarmaydi —
        // yetim fayl (bazada yozuvi yo'q) xavfsiz, hisobotda ko'rsatiladi.
        var deleted = 0;
        var errors = new List<string>();
        foreach (var path in storagePaths)
        {
            try
            {
                await storage.DeleteAsync(path, cancellationToken);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{path}: {ex.Message}");
                logger.LogWarning(ex, "purge-demo: fayl diskdan o'chmadi: {Path}", path);
            }
        }

        return new DemoPurgeReport
        {
            Found = report.Found,
            Anchor = report.Anchor,
            Counts = report.Counts,
            Blockers = report.Blockers,
            Applied = true,
            FilesDeleted = deleted,
            FileErrors = errors
        };
    }

    // ------------------------------------------------------------------ reja (identifikatsiya + to'siqlar)

    private async Task<Plan> BuildPlanAsync(CancellationToken ct)
    {
        var plan = new Plan();

        // 1. Langar — demo tyutor.
        var anchors = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Role == UserRole.Tutor && u.PhoneNumber == DemoDataSeeder.TutorPhone
                        && u.FullName == AnchorTutorName && u.CreatedBy == null)
            .Select(u => new { u.Id, u.HemisId, u.FacultyId, u.CreatedAt })
            .ToListAsync(ct);
        if (anchors.Count == 0)
            return plan;

        plan.Found = true;
        if (anchors.Count > 1)
            return plan.Block($"Demo tyutor belgilariga mos {anchors.Count} ta foydalanuvchi bor — kutilmagan holat, qo'lda tekshiring.");

        var anchor = anchors[0];
        plan.Anchor = $"demo tyutor {AnchorTutorName} ({DemoDataSeeder.TutorPhone}, Id {anchor.Id})";
        if (anchor.HemisId is not null && anchor.HemisId != DemoDataSeeder.TutorHemisId)
            return plan.Block($"Demo tyutor telefoni/FISH mos, lekin HEMIS ID '{anchor.HemisId}' (kutilgan {DemoDataSeeder.TutorHemisId}) — qo'lda tekshiring.");

        var userBatch = anchor.CreatedAt;

        // 2. Fakultetlar — langar fakulteti AT bo'lishi shart; qolganlari o'sha 1-paket vaqtida.
        var anchorFaculty = anchor.FacultyId is { } anchorFacultyId
            ? await db.Faculties.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.Id == anchorFacultyId)
                .Select(f => new { f.Id, f.Name, f.Code, f.CreatedAt, f.CreatedBy })
                .FirstOrDefaultAsync(ct)
            : null;
        if (anchorFaculty is null || anchorFaculty.CreatedBy is not null
            || (anchorFaculty.Name, anchorFaculty.Code) != DemoFaculties[0])
        {
            return plan.Block("Demo tyutorning fakulteti seed'dagi 'Axborot texnologiyalari' (AT) emas yoki admin yaratgan — " +
                              "demo ierarxiyani ishonchli aniqlab bo'lmadi, qo'lda tekshiring.");
        }

        var orgBatch = anchorFaculty.CreatedAt;
        var facultyCodes = DemoFaculties.Select(f => f.Code).ToArray();
        var faculties = (await db.Faculties.IgnoreQueryFilters().AsNoTracking()
                .Where(f => facultyCodes.Contains(f.Code) && f.CreatedAt == orgBatch && f.CreatedBy == null)
                .Select(f => new { f.Id, f.Name, f.Code })
                .ToListAsync(ct))
            .Where(f => DemoFaculties.Contains((f.Name, f.Code)))
            .ToList();
        plan.Faculties.UnionWith(faculties.Select(f => f.Id));
        var facultyCodeById = faculties.ToDictionary(f => f.Id, f => f.Code);
        var facultyNameById = faculties.ToDictionary(f => f.Id, f => $"{f.Name} ({f.Code})");

        // 3. Kafedra → yo'nalish → guruh: demo fakultet ichidagi HAR BIR yozuv seed'niki bo'lishi shart.
        var departments = await db.Departments.IgnoreQueryFilters().AsNoTracking()
            .Where(d => plan.Faculties.Contains(d.FacultyId))
            .Select(d => new { d.Id, d.FacultyId, d.Name, d.Code, d.CreatedAt, d.CreatedBy })
            .ToListAsync(ct);
        foreach (var d in departments)
        {
            var facultyCode = facultyCodeById[d.FacultyId];
            var seeded = d.CreatedBy is null && d.CreatedAt == orgBatch && DemoDepartments.Contains((facultyCode, d.Name, d.Code));
            var legacy = d.CreatedBy is null && d.Name == LegacyDepartmentName && d.Code == facultyCode;
            if (seeded || legacy)
                plan.Departments.Add(d.Id);
            else
                plan.Block("demo-faculty-content", $"Demo fakultet {facultyNameById[d.FacultyId]} ichida demo bo'lmagan kafedra: '{d.Name}' ({d.Code})");
        }

        var directions = await db.Directions.IgnoreQueryFilters().AsNoTracking()
            .Where(d => plan.Departments.Contains(d.DepartmentId))
            .Select(d => new { d.Id, d.Name, d.Code, d.CreatedAt, d.CreatedBy })
            .ToListAsync(ct);
        var directionCodeById = new Dictionary<Guid, string>();
        foreach (var d in directions)
        {
            if (d.CreatedBy is null && d.CreatedAt == orgBatch && DemoDirections.Any(x => x.Name == d.Name && x.Code == d.Code))
            {
                plan.Directions.Add(d.Id);
                directionCodeById[d.Id] = d.Code;
            }
            else
            {
                plan.Block("demo-faculty-content", $"Demo kafedra ichida demo bo'lmagan yo'nalish: '{d.Name}' ({d.Code})");
            }
        }

        var groups = await db.StudentGroups.IgnoreQueryFilters().AsNoTracking()
            .Where(g => plan.Directions.Contains(g.DirectionId))
            .Select(g => new { g.Id, g.DirectionId, g.Name, g.CreatedAt, g.CreatedBy })
            .ToListAsync(ct);
        var groupNameById = new Dictionary<Guid, string>();
        foreach (var g in groups)
        {
            var expected = DemoDirections.First(x => x.Code == directionCodeById[g.DirectionId]).Group;
            if (g.CreatedBy is null && g.CreatedAt == orgBatch && g.Name == expected)
            {
                plan.Groups.Add(g.Id);
                groupNameById[g.Id] = g.Name;
            }
            else
            {
                plan.Block("demo-faculty-content", $"Demo yo'nalish ichida demo bo'lmagan guruh: '{g.Name}'");
            }
        }

        // 4. Tyutorlar (2-paket).
        var tutorPhones = DemoTutors.Select(t => t.Phone).ToArray();
        var tutorCandidates = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Role == UserRole.Tutor && u.PhoneNumber != null && tutorPhones.Contains(u.PhoneNumber)
                        && u.CreatedAt == userBatch && u.CreatedBy == null)
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.HemisId, u.FacultyId })
            .ToListAsync(ct);
        foreach (var u in tutorCandidates)
        {
            var match = DemoTutors.FirstOrDefault(t => t.Phone == u.PhoneNumber && t.Name == u.FullName);
            if (match.Name is null || (u.HemisId is not null && u.HemisId != match.HemisId))
                continue;
            if (u.FacultyId is { } fid && facultyCodeById.TryGetValue(fid, out var code) && code == match.FacultyCode)
                plan.Tutors.Add(u.Id);
            else
                plan.Block("demo-tutor", $"Demo tyutor {u.FullName} ({u.PhoneNumber}) demo bo'lmagan fakultetga o'tkazilgan");
        }

        // 5. Talabalar — demo guruhdagi HAR BIR talaba seed'niki bo'lishi shart.
        var students = await (
                from p in db.StudentProfiles.IgnoreQueryFilters()
                join u in db.Users.IgnoreQueryFilters() on p.UserId equals u.Id
                where plan.Groups.Contains(p.StudentGroupId)
                select new { ProfileId = p.Id, p.UserId, p.HemisId, p.StudentGroupId, u.FullName, u.Role, u.FacultyId, u.CreatedAt, u.CreatedBy })
            .AsNoTracking()
            .ToListAsync(ct);
        foreach (var s in students)
        {
            var demo = s.Role == UserRole.Student && s.CreatedBy is null && s.CreatedAt == userBatch
                       && s.FacultyId == anchorFaculty.Id
                       && DemoStudents.TryGetValue(s.HemisId, out var expectedName) && expectedName == s.FullName;
            if (demo)
            {
                plan.Students.Add(s.UserId);
                plan.StudentProfiles.Add(s.ProfileId);
            }
            else
            {
                plan.Block("foreign-student", $"Demo guruh {groupNameById[s.StudentGroupId]} da demo bo'lmagan talaba: {s.FullName} (HEMIS {s.HemisId})");
            }
        }

        var demoUsers = plan.DemoUsers;

        // 6. Demo fakultetga bog'langan boshqa foydalanuvchilar (asosiy fakultet — FK'siz ustun).
        var foreignFacultyUsers = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.FacultyId != null && plan.Faculties.Contains(u.FacultyId.Value) && !demoUsers.Contains(u.Id))
            .Select(u => new { u.FullName, u.Role, u.FacultyId })
            .ToListAsync(ct);
        foreach (var u in foreignFacultyUsers)
            plan.Block("foreign-faculty-user", $"Demo fakultet {facultyNameById[u.FacultyId!.Value]} ga demo bo'lmagan foydalanuvchi biriktirilgan: {u.FullName} ({u.Role})");

        var tutorFaculties = await db.TutorFaculties.AsNoTracking()
            .Where(x => plan.Faculties.Contains(x.FacultyId) || plan.Tutors.Contains(x.TutorUserId))
            .Select(x => new { x.Id, x.TutorUserId, x.FacultyId })
            .ToListAsync(ct);
        foreach (var x in tutorFaculties)
        {
            if (plan.Tutors.Contains(x.TutorUserId) && plan.Faculties.Contains(x.FacultyId))
                plan.TutorFaculties.Add(x.Id);
            else if (!plan.Tutors.Contains(x.TutorUserId))
                plan.Block("foreign-faculty-user", $"Demo bo'lmagan tyutor (Id {x.TutorUserId}) demo fakultet {facultyNameById[x.FacultyId]} ga biriktirilgan (tutor_faculties)");
            else
                plan.Block("demo-tutor", $"Demo tyutor (Id {x.TutorUserId}) demo bo'lmagan fakultetga biriktirilgan (tutor_faculties, fakultet Id {x.FacultyId})");
        }

        var scopes = await db.TutorScopes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => plan.Tutors.Contains(x.TutorUserId) || plan.Faculties.Contains(x.FacultyId)
                        || (x.DepartmentId != null && plan.Departments.Contains(x.DepartmentId.Value))
                        || (x.DirectionId != null && plan.Directions.Contains(x.DirectionId.Value))
                        || (x.StudentGroupId != null && plan.Groups.Contains(x.StudentGroupId.Value)))
            .Select(x => new { x.Id, x.TutorUserId, x.FacultyId, x.DepartmentId, x.DirectionId, x.StudentGroupId })
            .ToListAsync(ct);
        foreach (var x in scopes)
        {
            var orgIsDemo = plan.Faculties.Contains(x.FacultyId)
                            && (x.DepartmentId is null || plan.Departments.Contains(x.DepartmentId.Value))
                            && (x.DirectionId is null || plan.Directions.Contains(x.DirectionId.Value))
                            && (x.StudentGroupId is null || plan.Groups.Contains(x.StudentGroupId.Value));
            if (plan.Tutors.Contains(x.TutorUserId) && orgIsDemo)
                plan.TutorScopes.Add(x.Id);
            else if (!plan.Tutors.Contains(x.TutorUserId))
                plan.Block("foreign-faculty-user", $"Demo bo'lmagan tyutor (Id {x.TutorUserId}) demo ierarxiyaga ko'lam bilan biriktirilgan (tutor_scopes)");
            else
                plan.Block("demo-tutor", $"Demo tyutor (Id {x.TutorUserId}) demo bo'lmagan fakultet/kafedra/yo'nalish/guruhga ko'lam bilan biriktirilgan (tutor_scopes)");
        }

        var assignments = await db.TutorAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => plan.Tutors.Contains(x.TutorUserId) || plan.Groups.Contains(x.StudentGroupId))
            .Select(x => new { x.Id, x.TutorUserId, x.StudentGroupId })
            .ToListAsync(ct);
        foreach (var x in assignments)
        {
            if (plan.Tutors.Contains(x.TutorUserId) && plan.Groups.Contains(x.StudentGroupId))
                plan.TutorAssignments.Add(x.Id);
            else if (!plan.Tutors.Contains(x.TutorUserId))
                plan.Block("foreign-faculty-user", $"Demo bo'lmagan tyutor (Id {x.TutorUserId}) demo guruh {groupNameById[x.StudentGroupId]} ga biriktirilgan (tutor_assignments)");
            else
                plan.Block("demo-tutor", $"Demo tyutor (Id {x.TutorUserId}) demo bo'lmagan guruhga biriktirilgan (tutor_assignments, guruh Id {x.StudentGroupId})");
        }

        // 7. Davrlar: demo nomli + CreatedBy null + faqat demo guruhlar (guruhsiz qolgan bo'lsa — demo talaba yozuvlari bor).
        var studentRowPeriods = await StudentRowPeriodIdsAsync(plan.Students, ct);
        var periodCandidates = await db.PracticePeriods.IgnoreQueryFilters().AsNoTracking()
            .Where(p => db.PracticePeriodGroups.Any(pg => pg.PeriodId == p.Id && plan.Groups.Contains(pg.StudentGroupId))
                        || (DemoPeriodNames.Contains(p.Name) && p.CreatedBy == null && studentRowPeriods.Contains(p.Id)))
            .Select(p => new { p.Id, p.Name, p.CreatedBy })
            .ToListAsync(ct);
        var periodCandidateIds = periodCandidates.Select(p => p.Id).ToList();
        var periodGroups = await db.PracticePeriodGroups.AsNoTracking()
            .Where(pg => periodCandidateIds.Contains(pg.PeriodId))
            .Select(pg => new { pg.Id, pg.PeriodId, pg.StudentGroupId })
            .ToListAsync(ct);
        foreach (var p in periodCandidates)
        {
            var own = periodGroups.Where(pg => pg.PeriodId == p.Id).ToList();
            var foreignGroups = own.Count(pg => !plan.Groups.Contains(pg.StudentGroupId));
            if (DemoPeriodNames.Contains(p.Name) && p.CreatedBy is null && foreignGroups == 0)
            {
                plan.Periods.Add(p.Id);
                plan.PeriodGroups.UnionWith(own.Select(pg => pg.Id));
            }
            else if (foreignGroups > 0 && DemoPeriodNames.Contains(p.Name) && p.CreatedBy is null)
            {
                plan.Block("period", $"Demo davr '{p.Name}' ga {foreignGroups} ta demo bo'lmagan guruh biriktirilgan");
            }
            else
            {
                plan.Block("period", $"Demo guruh(lar) demo bo'lmagan davrga biriktirilgan: '{p.Name}' (Id {p.Id})");
            }
        }

        // 8. Korxonalar (2-paket).
        var tins = DemoCompanies.Select(c => c.Tin).ToArray();
        var companies = await db.Companies.IgnoreQueryFilters().AsNoTracking()
            .Where(c => tins.Contains(c.Tin) && c.CreatedAt == userBatch && c.CreatedBy == null)
            .Select(c => new { c.Id, c.Name, c.Tin })
            .ToListAsync(ct);
        plan.Companies.UnionWith(companies.Where(c => DemoCompanies.Contains((c.Name, c.Tin))).Select(c => c.Id));

        // 9. Talabaga tegishli qatorlar.
        await CollectStudentRowsAsync(plan, ct);

        // 10. Demo foydalanuvchi "aktor" sifatida qatnashgan demo bo'lmagan yozuvlar.
        await CheckActorReferencesAsync(plan, ct);

        // 11. Fayllar, refresh token'lar, audit.
        await CollectFilesAsync(plan, ct);

        plan.RefreshTokens.UnionWith(await db.RefreshTokens.AsNoTracking()
            .Where(t => demoUsers.Contains(t.UserId)).Select(t => t.Id).ToListAsync(ct));

        var deletedIds = plan.AllEntityIds().Select(id => id.ToString()).ToList();
        plan.AuditLogs.UnionWith(await db.AuditLogs.AsNoTracking()
            .Where(a => (a.UserId != null && demoUsers.Contains(a.UserId.Value))
                        || (a.EntityId != null && deletedIds.Contains(a.EntityId)))
            .Select(a => a.Id)
            .ToListAsync(ct));

        return plan;
    }

    private async Task<HashSet<Guid>> StudentRowPeriodIdsAsync(HashSet<Guid> students, CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        ids.UnionWith(await db.PracticeApplications.Where(x => students.Contains(x.StudentUserId)).Select(x => x.PeriodId).Distinct().ToListAsync(ct));
        ids.UnionWith(await db.DailyAttendances.Where(x => students.Contains(x.StudentUserId)).Select(x => x.PeriodId).Distinct().ToListAsync(ct));
        ids.UnionWith(await db.DiaryEntries.Where(x => students.Contains(x.StudentUserId)).Select(x => x.PeriodId).Distinct().ToListAsync(ct));
        ids.UnionWith(await db.LeaveRequests.Where(x => students.Contains(x.StudentUserId)).Select(x => x.PeriodId).Distinct().ToListAsync(ct));
        ids.UnionWith(await db.PracticeGrades.Where(x => students.Contains(x.StudentUserId)).Select(x => x.PeriodId).Distinct().ToListAsync(ct));
        return ids;
    }

    private async Task CollectStudentRowsAsync(Plan plan, CancellationToken ct)
    {
        var s = plan.Students;
        var p = plan.Periods;
        var c = plan.Companies;

        void Classify(string table, IEnumerable<(Guid Id, Guid Student, Guid? Period)> rows, HashSet<Guid> target)
        {
            var foreignStudent = 0;
            var foreignPeriod = 0;
            foreach (var (id, student, period) in rows)
            {
                if (!s.Contains(student))
                    foreignStudent++;
                else if (period is { } pid && !p.Contains(pid))
                    foreignPeriod++;
                else
                    target.Add(id);
            }

            if (foreignStudent > 0)
                plan.Block("foreign-rows", $"{table}: demo davr/korxonaga tegishli {foreignStudent} ta demo bo'lmagan talaba yozuvi");
            if (foreignPeriod > 0)
                plan.Block("foreign-rows", $"{table}: demo talabaning demo bo'lmagan davrdagi {foreignPeriod} ta yozuvi");
        }

        var applications = await db.PracticeApplications.AsNoTracking()
            .Where(x => s.Contains(x.StudentUserId) || p.Contains(x.PeriodId) || c.Contains(x.CompanyId))
            .Select(x => new { x.Id, x.StudentUserId, x.PeriodId, x.ContractFileId })
            .ToListAsync(ct);
        Classify("practice_applications", applications.Select(x => (x.Id, x.StudentUserId, (Guid?)x.PeriodId)), plan.Applications);
        plan.ReferencedFiles.UnionWith(applications.Where(x => plan.Applications.Contains(x.Id) && x.ContractFileId != null).Select(x => x.ContractFileId!.Value));

        var attendances = await db.DailyAttendances.AsNoTracking()
            .Where(x => s.Contains(x.StudentUserId) || p.Contains(x.PeriodId))
            .Select(x => new { x.Id, x.StudentUserId, x.PeriodId, x.CheckInPhotoFileId, x.CheckOutPhotoFileId })
            .ToListAsync(ct);
        Classify("daily_attendances", attendances.Select(x => (x.Id, x.StudentUserId, (Guid?)x.PeriodId)), plan.Attendances);
        foreach (var x in attendances.Where(x => plan.Attendances.Contains(x.Id)))
        {
            if (x.CheckInPhotoFileId is { } a) plan.ReferencedFiles.Add(a);
            if (x.CheckOutPhotoFileId is { } b) plan.ReferencedFiles.Add(b);
        }

        // Hodisalarda davr yo'q — talaba yoki korxona bo'yicha.
        var events = await db.AttendanceEvents.AsNoTracking()
            .Where(x => s.Contains(x.StudentUserId) || c.Contains(x.CompanyId))
            .Select(x => new { x.Id, x.StudentUserId, x.PhotoFileId })
            .ToListAsync(ct);
        Classify("attendance_events", events.Select(x => (x.Id, x.StudentUserId, (Guid?)null)), plan.Events);
        plan.ReferencedFiles.UnionWith(events.Where(x => plan.Events.Contains(x.Id) && x.PhotoFileId != null).Select(x => x.PhotoFileId!.Value));

        var diaries = await db.DiaryEntries.AsNoTracking()
            .Where(x => s.Contains(x.StudentUserId) || p.Contains(x.PeriodId))
            .Select(x => new { x.Id, x.StudentUserId, x.PeriodId })
            .ToListAsync(ct);
        Classify("diary_entries", diaries.Select(x => (x.Id, x.StudentUserId, (Guid?)x.PeriodId)), plan.Diaries);

        var attachments = await db.DiaryAttachments.AsNoTracking()
            .Where(x => plan.Diaries.Contains(x.DiaryEntryId))
            .Select(x => new { x.Id, x.StoredFileId })
            .ToListAsync(ct);
        plan.DiaryAttachments.UnionWith(attachments.Select(x => x.Id));
        plan.ReferencedFiles.UnionWith(attachments.Select(x => x.StoredFileId));

        var leaves = await db.LeaveRequests.AsNoTracking()
            .Where(x => s.Contains(x.StudentUserId) || p.Contains(x.PeriodId))
            .Select(x => new { x.Id, x.StudentUserId, x.PeriodId, x.DocumentFileId })
            .ToListAsync(ct);
        Classify("leave_requests", leaves.Select(x => (x.Id, x.StudentUserId, (Guid?)x.PeriodId)), plan.Leaves);
        plan.ReferencedFiles.UnionWith(leaves.Where(x => plan.Leaves.Contains(x.Id) && x.DocumentFileId != null).Select(x => x.DocumentFileId!.Value));

        var grades = await db.PracticeGrades.AsNoTracking()
            .Where(x => s.Contains(x.StudentUserId) || p.Contains(x.PeriodId))
            .Select(x => new { x.Id, x.StudentUserId, x.PeriodId })
            .ToListAsync(ct);
        Classify("practice_grades", grades.Select(x => (x.Id, x.StudentUserId, (Guid?)x.PeriodId)), plan.Grades);
    }

    private async Task CheckActorReferencesAsync(Plan plan, CancellationToken ct)
    {
        var u = plan.DemoUsers;
        var s = plan.Students;

        void Check(string what, int count)
        {
            if (count > 0)
                plan.Block("actor", $"Demo foydalanuvchi {what} demo bo'lmagan {count} ta yozuv bor");
        }

        Check("qaror qilgan (practice_applications)", await db.PracticeApplications
            .CountAsync(x => x.DecidedByUserId != null && u.Contains(x.DecidedByUserId.Value) && !s.Contains(x.StudentUserId), ct));
        Check("qo'lda belgilagan davomat (daily_attendances)", await db.DailyAttendances
            .CountAsync(x => x.ManualByUserId != null && u.Contains(x.ManualByUserId.Value) && !s.Contains(x.StudentUserId), ct));
        Check("tekshirgan kundalik (diary_entries)", await db.DiaryEntries
            .CountAsync(x => x.ReviewedByUserId != null && u.Contains(x.ReviewedByUserId.Value) && !s.Contains(x.StudentUserId), ct));
        Check("qaror qilgan ruxsat (leave_requests)", await db.LeaveRequests
            .CountAsync(x => x.DecidedByUserId != null && u.Contains(x.DecidedByUserId.Value) && !s.Contains(x.StudentUserId), ct));
        Check("yakunlagan baho (practice_grades)", await db.PracticeGrades
            .CountAsync(x => x.FinalizedByUserId != null && u.Contains(x.FinalizedByUserId.Value) && !s.Contains(x.StudentUserId), ct));
        var periods = plan.Periods;
        Check("yaratgan davr (practice_periods)", await db.PracticePeriods.IgnoreQueryFilters()
            .CountAsync(x => u.Contains(x.CreatedByUserId) && !periods.Contains(x.Id), ct));
        Check("o'zgartirgan sozlama (app_settings)", await db.AppSettings
            .CountAsync(x => x.UpdatedByUserId != null && u.Contains(x.UpdatedByUserId.Value), ct));
    }

    private async Task CollectFilesAsync(Plan plan, CancellationToken ct)
    {
        var u = plan.DemoUsers;
        var uploadedByDemo = await db.StoredFiles.AsNoTracking()
            .Where(f => f.UploadedByUserId != null && u.Contains(f.UploadedByUserId.Value))
            .Select(f => f.Id)
            .ToListAsync(ct);

        var candidates = new HashSet<Guid>(uploadedByDemo);
        candidates.UnionWith(plan.ReferencedFiles);

        // Demo bo'lmagan (o'chirilmaydigan) yozuvlardan nomzod fayllarga havolalar.
        var foreign = new HashSet<Guid>();
        foreign.UnionWith(await db.DiaryAttachments
            .Where(x => candidates.Contains(x.StoredFileId) && !plan.DiaryAttachments.Contains(x.Id))
            .Select(x => x.StoredFileId).ToListAsync(ct));
        foreign.UnionWith(await db.PracticeApplications
            .Where(x => x.ContractFileId != null && candidates.Contains(x.ContractFileId.Value) && !plan.Applications.Contains(x.Id))
            .Select(x => x.ContractFileId!.Value).ToListAsync(ct));
        foreign.UnionWith(await db.DailyAttendances
            .Where(x => x.CheckInPhotoFileId != null && candidates.Contains(x.CheckInPhotoFileId.Value) && !plan.Attendances.Contains(x.Id))
            .Select(x => x.CheckInPhotoFileId!.Value).ToListAsync(ct));
        foreign.UnionWith(await db.DailyAttendances
            .Where(x => x.CheckOutPhotoFileId != null && candidates.Contains(x.CheckOutPhotoFileId.Value) && !plan.Attendances.Contains(x.Id))
            .Select(x => x.CheckOutPhotoFileId!.Value).ToListAsync(ct));
        foreign.UnionWith(await db.AttendanceEvents
            .Where(x => x.PhotoFileId != null && candidates.Contains(x.PhotoFileId.Value) && !plan.Events.Contains(x.Id))
            .Select(x => x.PhotoFileId!.Value).ToListAsync(ct));
        foreign.UnionWith(await db.LeaveRequests
            .Where(x => x.DocumentFileId != null && candidates.Contains(x.DocumentFileId.Value) && !plan.Leaves.Contains(x.Id))
            .Select(x => x.DocumentFileId!.Value).ToListAsync(ct));
        foreign.UnionWith(await db.DocumentTemplates.IgnoreQueryFilters()
            .Where(x => candidates.Contains(x.FileId))
            .Select(x => x.FileId).ToListAsync(ct));

        var uploadedSet = uploadedByDemo.ToHashSet();
        var blockedUploads = foreign.Count(uploadedSet.Contains);
        if (blockedUploads > 0)
            plan.Block("files", $"Demo foydalanuvchi yuklagan {blockedUploads} ta fayl demo bo'lmagan yozuvda ishlatilgan (stored_files)");

        // Boshqa yuklagan, lekin faqat demo qatorlar ishlatgan fayl — o'chiriladi; boshqa joyda ham ishlatilgani — qoladi.
        candidates.ExceptWith(foreign);
        var files = await db.StoredFiles.AsNoTracking()
            .Where(f => candidates.Contains(f.Id))
            .Select(f => new { f.Id, f.StoragePath })
            .ToListAsync(ct);
        plan.Files.UnionWith(files.Select(f => f.Id));
        plan.StoragePaths.AddRange(files.Select(f => f.StoragePath));
    }

    // ------------------------------------------------------------------ bajarish

    private async Task ExecuteAsync(Plan plan, CancellationToken ct)
    {
        // Tartib: bolalar → ota-onalar (Restrict FK'lar). Har DELETE soni rejadagiga teng bo'lishi shart — aks holda
        // reja tuzilgandan keyin baza o'zgargan: istisno → tranzaksiya rollback.
        async Task Delete(string table, HashSet<Guid> ids, Func<List<Guid>, Task<int>> run)
        {
            if (ids.Count == 0)
                return;
            var deleted = await run([.. ids]);
            if (deleted != ids.Count)
                throw new InvalidOperationException($"purge-demo: {table} — rejada {ids.Count}, o'chdi {deleted}. Tranzaksiya qaytarildi, qayta urinib ko'ring.");
        }

        await Delete("audit_logs", plan.AuditLogs, ids => db.AuditLogs.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("diary_attachments", plan.DiaryAttachments, ids => db.DiaryAttachments.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("attendance_events", plan.Events, ids => db.AttendanceEvents.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("daily_attendances", plan.Attendances, ids => db.DailyAttendances.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("leave_requests", plan.Leaves, ids => db.LeaveRequests.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("diary_entries", plan.Diaries, ids => db.DiaryEntries.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("practice_grades", plan.Grades, ids => db.PracticeGrades.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("practice_applications", plan.Applications, ids => db.PracticeApplications.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("stored_files", plan.Files, ids => db.StoredFiles.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("practice_period_groups", plan.PeriodGroups, ids => db.PracticePeriodGroups.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("practice_periods", plan.Periods, ids => db.PracticePeriods.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("tutor_assignments", plan.TutorAssignments, ids => db.TutorAssignments.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("tutor_scopes", plan.TutorScopes, ids => db.TutorScopes.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("tutor_faculties", plan.TutorFaculties, ids => db.TutorFaculties.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("refresh_tokens", plan.RefreshTokens, ids => db.RefreshTokens.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("student_profiles", plan.StudentProfiles, ids => db.StudentProfiles.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("users", plan.DemoUsers, ids => db.Users.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("companies", plan.Companies, ids => db.Companies.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("student_groups", plan.Groups, ids => db.StudentGroups.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("directions", plan.Directions, ids => db.Directions.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("departments", plan.Departments, ids => db.Departments.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));
        await Delete("faculties", plan.Faculties, ids => db.Faculties.IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct));

        // Purge'ning o'zi audit'da qoladi (demo yozuvlarining audit'i o'chirildi — ular endi mavjud bo'lmagan
        // obyektlarga ishora qiladi va demo shaxs ma'lumotini saqlaydi).
        var counts = plan.Counts().Where(c => c.Count > 0).ToDictionary(c => c.Table, c => c.Count);
        db.AuditLogs.Add(AuditLog.Record(
            AuditAction.Deleted, "DemoData", entityId: null,
            changes: JsonSerializer.Serialize(counts),
            reason: "purge-demo CLI: demo seed ma'lumoti o'chirildi",
            occurredAt: clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private static Dictionary<string, string> BuildStudentCatalog()
    {
        var catalog = DemoDataSeeder.MainStudents.ToDictionary(s => s.HemisId, s => s.Name);
        for (var i = 0; i < DemoDataSeeder.ExtraNames.Length; i++)
            catalog[(341040 + i).ToString(CultureInfo.InvariantCulture)] = DemoDataSeeder.ExtraNames[i];
        return catalog;
    }

    private sealed class Plan
    {
        private readonly Dictionary<string, List<string>> _blockers = [];

        public bool Found { get; set; }
        public string? Anchor { get; set; }

        public HashSet<Guid> Faculties { get; } = [];
        public HashSet<Guid> Departments { get; } = [];
        public HashSet<Guid> Directions { get; } = [];
        public HashSet<Guid> Groups { get; } = [];
        public HashSet<Guid> Tutors { get; } = [];
        public HashSet<Guid> Students { get; } = [];
        public HashSet<Guid> StudentProfiles { get; } = [];
        public HashSet<Guid> TutorFaculties { get; } = [];
        public HashSet<Guid> TutorScopes { get; } = [];
        public HashSet<Guid> TutorAssignments { get; } = [];
        public HashSet<Guid> Periods { get; } = [];
        public HashSet<Guid> PeriodGroups { get; } = [];
        public HashSet<Guid> Companies { get; } = [];
        public HashSet<Guid> Applications { get; } = [];
        public HashSet<Guid> Attendances { get; } = [];
        public HashSet<Guid> Events { get; } = [];
        public HashSet<Guid> Diaries { get; } = [];
        public HashSet<Guid> DiaryAttachments { get; } = [];
        public HashSet<Guid> Leaves { get; } = [];
        public HashSet<Guid> Grades { get; } = [];
        public HashSet<Guid> ReferencedFiles { get; } = [];
        public HashSet<Guid> Files { get; } = [];
        public List<string> StoragePaths { get; } = [];
        public HashSet<Guid> RefreshTokens { get; } = [];
        public HashSet<Guid> AuditLogs { get; } = [];

        public HashSet<Guid> DemoUsers => [.. Tutors, .. Students];

        public IReadOnlyCollection<string> Blockers => [.. _blockers.Values.SelectMany(Summarize)];

        public Plan Block(string message) => Block("general", message);

        public Plan Block(string category, string message)
        {
            if (!_blockers.TryGetValue(category, out var list))
                _blockers[category] = list = [];
            list.Add(message);
            return this;
        }

        private static IEnumerable<string> Summarize(List<string> messages)
        {
            foreach (var message in messages.Take(MaxListed))
                yield return message;
            if (messages.Count > MaxListed)
                yield return $"... va yana {messages.Count - MaxListed} ta shunga o'xshash";
        }

        public IEnumerable<Guid> AllEntityIds() =>
            Faculties.Concat(Departments).Concat(Directions).Concat(Groups).Concat(DemoUsers).Concat(StudentProfiles)
                .Concat(TutorFaculties).Concat(TutorScopes).Concat(TutorAssignments).Concat(Periods).Concat(PeriodGroups)
                .Concat(Companies).Concat(Applications).Concat(Attendances).Concat(Events).Concat(Diaries)
                .Concat(DiaryAttachments).Concat(Leaves).Concat(Grades).Concat(Files).Concat(RefreshTokens);

        public IEnumerable<DemoPurgeTableCount> Counts() =>
        [
            new("users", Tutors.Count + Students.Count),
            new("student_profiles", StudentProfiles.Count),
            new("refresh_tokens", RefreshTokens.Count),
            new("faculties", Faculties.Count),
            new("departments", Departments.Count),
            new("directions", Directions.Count),
            new("student_groups", Groups.Count),
            new("tutor_faculties", TutorFaculties.Count),
            new("tutor_scopes", TutorScopes.Count),
            new("tutor_assignments", TutorAssignments.Count),
            new("companies", Companies.Count),
            new("practice_periods", Periods.Count),
            new("practice_period_groups", PeriodGroups.Count),
            new("practice_applications", Applications.Count),
            new("daily_attendances", Attendances.Count),
            new("attendance_events", Events.Count),
            new("diary_entries", Diaries.Count),
            new("diary_attachments", DiaryAttachments.Count),
            new("leave_requests", Leaves.Count),
            new("practice_grades", Grades.Count),
            new("stored_files", Files.Count),
            new("audit_logs", AuditLogs.Count)
        ];

        public DemoPurgeReport ToReport(bool applied) => new()
        {
            Found = Found,
            Anchor = Anchor,
            Counts = [.. Counts()],
            Blockers = [.. Blockers],
            Applied = applied
        };
    }
}

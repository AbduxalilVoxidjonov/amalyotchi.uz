using System.Globalization;
using System.Text;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Infrastructure.Persistence.Seeding;

/// <summary>Demo ma'lumot (<c>Seed:Demo=true</c> — faqat demo/stend uchun) — frontend mock'lariga mos:
/// AT fakulteti, 412-22/413-22 guruhlari, tyutor Nodira Saidova, 38 talaba, 6 korxona, joriy amaliyot davri,
/// so'nggi ish kunlari davomati (+ har check-in/check-out uchun <see cref="AttendanceEvent"/>, bugun 3 ta radius
/// tashqarisidagi rad etilgan urinish — tyutor xaritasi va ogohlantirishlar uchun), kundaliklar, ruxsat so'rovlari va baholar.
/// Idempotent: demo tyutor mavjud bo'lsa faqat yetishmayotgan davomat hodisalari to'ldiriladi (eski seed'dan qolgan baza),
/// boshqa hech narsa qilinmaydi. Barcha sanalar <see cref="PracticeTime"/> bo'yicha.</summary>
public sealed class DemoDataSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IClock clock,
    IFileStorage storage,
    ILogger<DemoDataSeeder> logger)
{
    public const string TutorPhone = "+998907654321";
    public const string TutorHemisId = "100000000002";
    public const string TutorPassword = "tutor12345";

    /// <summary>Brauzer orqali (HEMIS ID + parol) kirishni darhol sinash uchun demo talaba — Aliyev Akmal (412-22).
    /// Parol "o'zi o'rnatgan" kabi saqlanadi (<c>MustChangePassword=false</c>).</summary>
    public const string StudentHemisId = "341030";
    public const string StudentPassword = "talaba12345";
    public const string PeriodName = "Ishlab chiqarish amaliyoti 2026";

    /// <summary>Demo ikkinchi davr — bir o'quv yilida kuzgi + bahorgi davr stsenariysi uchun.</summary>
    public const string SpringPeriodName = "Bahorgi amaliyot 2027";
    public static readonly DateOnly SpringPeriodStart = new(2027, 2, 1);
    public static readonly DateOnly SpringPeriodEnd = new(2027, 3, 15);

    /// <summary>Talabalar hech qachon bir xil raqam olmasin (unique index) — 341030..341035 asosiylar uchun.</summary>
    private const long FirstTelegramId = 100_000_001;

    /// <summary>Bugun belgilanmaydigan va radius tashqarisidan rad etilgan urinish qiladigan "shubhali" talabalar
    /// (HEMIS ID): Rahimov Sardor, Abdullayev Jasur, Boboyeva Madina — tyutor xaritasida "bad" nuqtalar.</summary>
    private static readonly string[] SuspiciousHemisIds = ["341034", "341040", "341041"];

    private static readonly (string Name, string HemisId, int GroupIndex, int CompanyIndex)[] MainStudents =
    [
        ("Aliyev Akmal", "341030", 0, 0),
        ("Karimov Bekzod", "341031", 0, 1),
        ("Sobirov Diyor", "341032", 1, 2),
        ("Yusupova Nilufar", "341033", 1, 3),
        ("Rahimov Sardor", "341034", 0, 4),
        ("Toshpulatova Zarina", "341035", 1, 5)
    ];

    private static readonly string[] ExtraNames =
    [
        "Abdullayev Jasur", "Boboyeva Madina", "Ergashev Sherzod", "Fayzullayeva Kamola", "Gʻaniyev Otabek",
        "Hasanova Dilnoza", "Ismoilov Javohir", "Jalilova Sevara", "Kamolov Bobur", "Latipova Gulnora",
        "Mahmudov Umid", "Nurmatova Feruza", "Olimov Doston", "Primova Nigora", "Qosimov Rustam",
        "Rasulova Shahnoza", "Saidov Alisher", "Tursunova Malika", "Umarov Farrux", "Valiyeva Zulfiya",
        "Xolmatov Sanjar", "Yoqubova Nilufar", "Zokirov Islom", "Axmedova Laylo", "Berdiyev Shohrux",
        "Choriyeva Mohira", "Davronov Temur", "Eshonova Dildora", "Farhodov Aziz", "Gulomova Sitora",
        "Hakimov Bekzod", "Ibrohimova Nodira"
    ];

    /// <summary>Qo'shimcha talabalar uchun korxonalar (Qurilish Trest — faqat Rahimov Sardor).</summary>
    private static readonly int[] ExtraCompanyCycle = [0, 1, 2, 3, 5];

    private static readonly string[] DiaryTopics =
    [
        "loyiha kodini o'rganish va muhitni sozlash",
        "ma'lumotlar bazasi sxemasini tahlil qilish",
        "API endpoint'larini yozish va sinovdan o'tkazish",
        "mijoz talablarini yig'ish va hujjatlashtirish",
        "xatolarni tuzatish (bug fix) va code review",
        "hisobot shakllarini tayyorlash",
        "tizim xavfsizligi bo'yicha tekshiruv",
        "mentor bilan haftalik natijalarni muhokama qilish"
    ];

    private static readonly string[] Learned =
    [
        "Git branch'lar bilan ishlash va pull request tartibi",
        "SQL so'rovlarini optimallashtirish",
        "Mijoz bilan muloqot va talablarni aniqlashtirish",
        "Unit testlar yozish amaliyoti",
        "Loyiha hujjatlarini rasmiylashtirish"
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var today = PracticeTime.LocalDate(clock.UtcNow);
        var random = new Random(42);

        if (await db.Users.AnyAsync(u => u.PhoneNumber == TutorPhone, cancellationToken))
        {
            await TopUpAttendanceEventsAsync(today, random, cancellationToken);
            await EnsureDemoStudentPasswordAsync(cancellationToken);
            logger.LogInformation("Demo seed: demo tyutor ({Phone}) allaqachon mavjud — o'tkazib yuborildi", TutorPhone);
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin, cancellationToken)
            ?? throw new InvalidOperationException("Demo seed: admin topilmadi — avval DbSeeder ishlashi kerak.");

        // 1. Tashkilot — faol o'quv yili DbSeeder tomonidan allaqachon yaratilgan (avval shu ishlashi shart).
        var year = await db.AcademicYears.FirstOrDefaultAsync(y => y.IsActive, cancellationToken);
        var yearIsNew = year is null;
        if (year is null)
        {
            year = AcademicYear.Create("2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
            year.Activate();
        }

        var it = Faculty.Create("Axborot texnologiyalari", "AT");
        var economics = Faculty.Create("Iqtisodiyot va moliya", "IM");
        var construction = Faculty.Create("Qurilish va arxitektura", "QA");
        var philology = Faculty.Create("Filologiya", "FL");

        var itSoftware = it.AddDepartment("Dasturiy injiniring kafedrasi", "SE");
        var itComputer = it.AddDepartment("Kompyuter injiniringi kafedrasi", "CE");
        var economicsBanking = economics.AddDepartment("Moliya va bank ishi kafedrasi", "FB");
        var constructionEngineering = construction.AddDepartment("Qurilish muhandisligi kafedrasi", "CM");

        var group412 = itSoftware.AddDirection("Dasturiy injiniring", "60610500").AddGroup("412-22", 3, year.Id);
        var group413 = itComputer.AddDirection("Kompyuter injiniringi", "60610400").AddGroup("413-22", 3, year.Id);
        var group221 = economicsBanking.AddDirection("Bank ishi", "60410100").AddGroup("221-23", 2, year.Id);
        var group318 = constructionEngineering.AddDirection("Qurilish muhandisligi", "60730100").AddGroup("318-21", 4, year.Id);

        if (yearIsNew)
            db.AcademicYears.Add(year);
        db.Faculties.AddRange(it, economics, construction, philology);
        await db.SaveChangesAsync(cancellationToken);

        // 2. Tyutorlar + biriktiruvlar
        var tutorHash = passwordHasher.Hash(TutorPassword);
        var tutor = User.CreateWithPassword("Nodira Saidova", TutorHemisId, TutorPhone, tutorHash, UserRole.Tutor, it.Id);
        var tutorEconomics = User.CreateWithPassword("Baxtiyor Rasulov", "100000000003", "+998912445102", tutorHash, UserRole.Tutor, economics.Id);
        var tutorConstruction = User.CreateWithPassword("Dilshod Ergashev", "100000000004", "+998937001845", tutorHash, UserRole.Tutor, construction.Id);
        db.Users.AddRange(tutor, tutorEconomics, tutorConstruction);
        db.TutorAssignments.AddRange(
            TutorAssignment.Create(tutor.Id, group412.Id, year.Id),
            TutorAssignment.Create(tutor.Id, group413.Id, year.Id),
            TutorAssignment.Create(tutorEconomics.Id, group221.Id, year.Id),
            TutorAssignment.Create(tutorConstruction.Id, group318.Id, year.Id));
        // Ko'lamlar (guruh darajasi) — biriktiruvlarning "nima tanlangan" manbai; ikkalasi mos bo'lishi shart.
        db.TutorScopes.AddRange(
            TutorScope.Create(tutor.Id, TutorScopeLevel.Group, it.Id, itSoftware.Id, group412.DirectionId, group412.Id),
            TutorScope.Create(tutor.Id, TutorScopeLevel.Group, it.Id, itComputer.Id, group413.DirectionId, group413.Id),
            TutorScope.Create(tutorEconomics.Id, TutorScopeLevel.Group, economics.Id, economicsBanking.Id, group221.DirectionId, group221.Id),
            TutorScope.Create(tutorConstruction.Id, TutorScopeLevel.Group, construction.Id, constructionEngineering.Id, group318.DirectionId, group318.Id));

        // 3. Korxonalar (Toshkent). Har biri Create paytida o'z check-in QR tokenini oladi (AMLQR:1:{token}).
        var companies = new[]
        {
            Company.Create("Tech Solutions MChJ", "304512889", "Dasturiy ta'minot ishlab chiqish",
                "Toshkent, Amir Temur ko'chasi 108", new GeoPoint(41.3111, 69.2797), 150,
                "Islomov B.", "+998901112233", "Xolmatov S.", "+998935551209"),
            Company.Create("Agrobank ATB", "201344712", "Bank xizmatlari",
                "Toshkent, Mustaqillik 12", new GeoPoint(41.3123, 69.2787), 120,
                "Karimova D.", "+998901112244"),
            Company.Create("Uzinfocom", "203112340", "IT infratuzilma",
                "Toshkent, Bunyodkor shoh ko'chasi 32", new GeoPoint(41.3260, 69.2280), 150,
                "Rasulov A.", "+998901112255", "Nazarov T.", "+998901112266"),
            Company.Create("Mediapark", "305447120", "Elektronika savdosi",
                "Toshkent, Yunusobod 4-mavze", new GeoPoint(41.3265, 69.2285), 150,
                "Yusupov M.", "+998901112277"),
            Company.Create("Qurilish Trest 12", "305881204", "Qurilish",
                "Toshkent v., Zangiota", new GeoPoint(41.2995, 69.2401), 450,
                "Ergashev Q.", "+998901112288"),
            Company.Create("Ipak Yuli Bank", "200945187", "Bank xizmatlari",
                "Toshkent, Abdulla Qodiriy 1", new GeoPoint(41.3060, 69.2610), 150,
                "Sodiqova N.", "+998901112299", "Olimov B.", "+998901112200")
        };
        db.Companies.AddRange(companies);

        // 4. Talabalar (38): 6 asosiy + 32 qo'shimcha
        var groups = new[] { group412, group413 };
        var students = new List<DemoStudent>(MainStudents.Length + ExtraNames.Length);

        for (var i = 0; i < MainStudents.Length; i++)
        {
            var (name, hemisId, groupIndex, companyIndex) = MainStudents[i];
            var phone = "+99890100000" + (i + 1).ToString(CultureInfo.InvariantCulture);
            var user = User.CreateStudent(name, it.Id, phone);
            user.LinkTelegram(FirstTelegramId + i, phone);
            if (hemisId == StudentHemisId)
                user.SetPasswordHash(passwordHasher.Hash(StudentPassword));
            var profile = StudentProfile.Create(user.Id, hemisId, groups[groupIndex].Id);
            students.Add(new DemoStudent(user, profile, companies[companyIndex], i));
        }

        for (var i = 0; i < ExtraNames.Length; i++)
        {
            var user = User.CreateStudent(ExtraNames[i], it.Id);
            var hemisId = (341040 + i).ToString(CultureInfo.InvariantCulture);
            var profile = StudentProfile.Create(user.Id, hemisId, groups[i % 2].Id);
            students.Add(new DemoStudent(user, profile, companies[ExtraCompanyCycle[i % ExtraCompanyCycle.Length]], MainStudents.Length + i));
        }

        db.Users.AddRange(students.Select(s => s.User));
        db.StudentProfiles.AddRange(students.Select(s => s.Profile));
        await db.SaveChangesAsync(cancellationToken);

        // 5. Joriy amaliyot davri
        var period = PracticePeriod.Create(
            PeriodName, year.Id, today.AddDays(-14), today.AddDays(30), admin.Id,
            CheckInRules.Default, WorkDays.MondayToSaturday, requiredDays: 36, dailyReportRequired: true);
        period.AttachGroup(group412.Id);
        period.AttachGroup(group413.Id);
        period.Activate();
        db.PracticePeriods.Add(period);

        // 5a. Ikkinchi (bahorgi) davr — bir o'quv yilida ikki davr stsenariysi: o'sha guruhlar, hali boshlanmagan
        // (ko'rinadigan holat "planned"; saqlanadigan holat admin API'dagidek ochiq — Active). Faqat bo'sh bazada
        // (demo seed birinchi marta) va joriy davr bilan kesishmasa qo'shiladi.
        if (SpringPeriodStart > period.EndDate)
        {
            var spring = PracticePeriod.Create(
                SpringPeriodName, year.Id, SpringPeriodStart, SpringPeriodEnd, admin.Id,
                CheckInRules.Default, WorkDays.MondayToSaturday,
                requiredDays: PracticePeriod.CountWorkDays(SpringPeriodStart, SpringPeriodEnd, WorkDays.MondayToSaturday, _ => false),
                dailyReportRequired: true);
            spring.AttachGroup(group412.Id);
            spring.AttachGroup(group413.Id);
            spring.Activate();
            db.PracticePeriods.Add(spring);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 6. Arizalar: 30 tasdiqlangan (6 asosiy + 24), 4 yangi, 2 qaytarilgan, 2 rad etilgan
        var counts = new Dictionary<ApplicationStatus, int>();
        foreach (var student in students)
        {
            var submittedAt = At(period.StartDate.AddDays(-7 + (student.Index % 5)), new TimeOnly(10, 0));
            var decidedAt = submittedAt.AddHours(20 + (student.Index % 30));
            var application = PracticeApplication.Create(
                student.User.Id, period.Id, student.Company.Id, student.Company.RadiusM, null, submittedAt);

            var extraIndex = student.Index - MainStudents.Length;
            switch (extraIndex)
            {
                case < 24:
                    application.Approve(tutor.Id, student.Company.RadiusM, Enumerable.Range(0, PracticeApplication.ChecklistItemCount),
                        "Hujjatlar to'liq", decidedAt);
                    break;
                case < 28:
                    break; // Submitted — tyutor kutmoqda
                case < 30:
                    application.ReturnForRevision(tutor.Id, "Shartnomada imzo yo'q", decidedAt);
                    break;
                default:
                    application.Reject(tutor.Id, "Korxona amaliyot yo'nalishiga mos emas", decidedAt);
                    break;
            }

            student.Application = application;
            counts[application.Status] = counts.GetValueOrDefault(application.Status) + 1;
            db.PracticeApplications.Add(application);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 7. Davomat + kundalik (davr boshidan bugungacha, ish kunlari)
        var workDays = new List<DateOnly>();
        for (var date = period.StartDate; date <= today; date = date.AddDays(1))
        {
            if (period.IsWorkDay(date, isHoliday: false))
                workDays.Add(date);
        }

        var pastWorkDays = workDays.Where(d => d < today).ToList();
        var yusupova = students[3];
        var rahimov = students[4];
        var excusedDay = pastWorkDays.Count > 0 ? pastWorkDays[^1] : (DateOnly?)null;
        var suspiciousDays = pastWorkDays.TakeLast(6).Where((_, i) => i % 2 == 0).ToHashSet();

        var attendanceCount = 0;
        var diaryCount = 0;
        var rewriteCount = 0;

        foreach (var student in students.Where(s => s.Application!.Status == ApplicationStatus.Approved))
        {
            var isSobirov = student.Index == 2;
            var isAliyev = student.Index == 0;
            var isKarimov = student.Index == 1;
            var isRahimov = student.Index == 4;
            var isYusupova = student.Index == 3;
            var presentRate = isSobirov ? 0.6 : 0.9;

            foreach (var date in workDays)
            {
                var isToday = date == today;
                if (isYusupova && date == excusedDay)
                    continue; // ruxsat tasdiqlangan kun — davomat qatori ruxsat bilan yoziladi

                var roll = random.NextDouble();
                var attends = roll < presentRate
                    || (isToday && (isAliyev || isKarimov))
                    || (isRahimov && suspiciousDays.Contains(date));
                if (!attends || (isToday && SuspiciousHemisIds.Contains(student.Profile.HemisId)))
                    continue; // shubhalilar bugun belgilanmaydi — radius tashqarisidan rad etilgan urinish (10-qadam)

                var isLate = !isToday && random.NextDouble() < 0.09;
                var distance = isRahimov && suspiciousDays.Contains(date) ? 410 : 20 + random.Next(0, 60);
                var accuracy = 8 + random.Next(0, 20);
                var checkInAt = At(date, new TimeOnly(isLate ? 9 : 8, isLate ? 41 : 50 + random.Next(0, 10)));

                var attendance = DailyAttendance.CheckIn(
                    student.User.Id, period.Id, date, checkInAt, distance, accuracy, CheckInVerdict.Accept(isLate));
                if (!isToday)
                    attendance.CheckOut(At(date, new TimeOnly(17, random.Next(0, 15))), 20 + random.Next(0, 60));
                if (isRahimov && suspiciousDays.Contains(date))
                    attendance.MarkSuspicious("Radius chetida: 410 m");

                db.DailyAttendances.Add(attendance);
                attendanceCount++;

                var writesDiary = isToday ? isAliyev || (!isKarimov && random.NextDouble() < 0.5) : random.NextDouble() < 0.85;
                if (!writesDiary)
                    continue;

                var dayNumber = workDays.IndexOf(date) + 1;
                var entry = DiaryEntry.Create(
                    student.User.Id, period.Id, date,
                    DiaryText(dayNumber, student.Company.Name, DiaryTopics[(dayNumber + student.Index) % DiaryTopics.Length]),
                    Learned[(dayNumber + student.Index) % Learned.Length],
                    At(date, new TimeOnly(18, 30)));

                if (!isToday)
                {
                    var reviewedAt = At(date.AddDays(1), new TimeOnly(11, 0));
                    var reviewRoll = random.NextDouble();
                    if (reviewRoll < 0.5)
                    {
                        var score = isYusupova ? 5 : 3 + random.Next(0, 3);
                        entry.Approve(tutor.Id, score, score >= 5 ? "Ajoyib hisobot" : null, reviewedAt);
                    }
                    else if (reviewRoll < 0.75)
                    {
                        entry.MarkSeen(tutor.Id, reviewedAt);
                    }
                    else if (rewriteCount < 2 && reviewRoll > 0.95)
                    {
                        entry.RequestRewrite(tutor.Id, "Batafsilroq yozing", reviewedAt);
                        rewriteCount++;
                    }
                }

                db.DiaryEntries.Add(entry);
                diaryCount++;
            }
        }

        // 8. Ruxsat so'rovlari: 2 kutilmoqda, 1 tasdiqlangan (+ davomat "sababli"), 1 rad etilgan
        var sobirov = students[2];
        var karimov = students[1];
        var decisionAt = At(today.AddDays(-1), new TimeOnly(12, 0));

        var leaves = new List<LeaveRequest>
        {
            LeaveRequest.Create(sobirov.User.Id, period.Id, today.AddDays(2), today.AddDays(2),
                "Kasallik — poliklinika spravkasi", "spravka.pdf"),
            LeaveRequest.Create(karimov.User.Id, period.Id, today.AddDays(3), today.AddDays(4),
                "Oilaviy sabab", "ariza.pdf")
        };

        if (excusedDay is { } excused)
        {
            var approved = LeaveRequest.Create(yusupova.User.Id, period.Id, excused, excused,
                "Universitet konferensiyasi", "xat.pdf");
            approved.Approve(tutor.Id, "Tasdiqlandi", At(excused.AddDays(-1), new TimeOnly(15, 0)));
            leaves.Add(approved);
            db.DailyAttendances.Add(DailyAttendance.Excuse(yusupova.User.Id, period.Id, excused, approved.Id));
            attendanceCount++;

            var rejected = LeaveRequest.Create(rahimov.User.Id, period.Id, excused, excused, "Sabab ko'rsatilmagan — shaxsiy");
            rejected.Reject(tutor.Id, "Sabab yetarli emas", decisionAt);
            leaves.Add(rejected);
        }

        db.LeaveRequests.AddRange(leaves);

        // 9. Baholar (yakunlanmagan)
        var grades = new (DemoStudent Student, int? Tutor, int? Reference)[]
        {
            (yusupova, 19, 10), (students[0], 18, 10), (students[5], 16, 8), (rahimov, 13, 7), (sobirov, null, null)
        };
        foreach (var (student, tutorPoints, referencePoints) in grades)
        {
            var grade = PracticeGrade.Create(student.User.Id, period.Id);
            grade.SetTutorPoints(tutorPoints);
            grade.SetReferencePoints(referencePoints);
            db.PracticeGrades.Add(grade);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 10. Davomat hodisalari: har check-in/check-out uchun qabul qilingan urinish + bugun 3 ta radius tashqarisi
        var events = await SeedAttendanceEventsAsync(period, today, random, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Demo seed: {Students} talaba, {Companies} korxona, arizalar {Applications}, {Attendance} davomat, {Accepted} qabul + {Rejected} rad hodisa, {Diaries} kundalik, {Leaves} ruxsat, {Grades} baho",
            students.Count, companies.Length,
            string.Join(", ", counts.Select(c => $"{c.Key}={c.Value}")),
            attendanceCount, events.Accepted, events.Rejected, diaryCount, leaves.Count, grades.Length);
    }

    /// <summary>Eski seed'dan qolgan baza: demo talabada (<see cref="StudentHemisId"/>) parol bo'lmasa — o'rnatiladi
    /// (brauzer login'ini sinash uchun). Parol allaqachon bor bo'lsa (o'zi o'zgartirgan bo'lishi mumkin) tegilmaydi.</summary>
    private async Task EnsureDemoStudentPasswordAsync(CancellationToken cancellationToken)
    {
        var student = await db.Users
            .FirstOrDefaultAsync(
                u => u.Role == UserRole.Student && u.StudentProfile != null && u.StudentProfile.HemisId == StudentHemisId,
                cancellationToken);
        if (student is null || student.PasswordHash is not null)
            return;

        student.SetPasswordHash(passwordHasher.Hash(StudentPassword));
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Demo seed: demo talaba ({HemisId}) uchun parol o'rnatildi", StudentHemisId);
    }

    /// <summary>Eski seed'dan qolgan baza (hodisalarsiz davomat): demo davri bor, lekin davomat qatorlarining
    /// bir qismida <see cref="AttendanceEvent"/> yo'q bo'lsa — yetishmayotganlari to'ldiriladi.
    /// Nazorat (talaba, sana, tur) bo'yicha: stendda haqiqiy check-in qilingan kunlar qayta yozilmaydi,
    /// qolgan kunlar esa koordinatasiz qolmaydi (profildagi kundalik jadvalda "Lokatsiya" bo'sh ko'rinmasin).</summary>
    private async Task TopUpAttendanceEventsAsync(DateOnly today, Random random, CancellationToken cancellationToken)
    {
        var period = await db.PracticePeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == PeriodName, cancellationToken);
        if (period is null)
            return;

        var existing = await LoadExistingEventKeysAsync(period.StartDate, cancellationToken);
        var events = await SeedAttendanceEventsAsync(period, today, random, cancellationToken, existing);
        if (events.Accepted > 0 || events.Rejected > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Demo seed: davomat hodisalari to'ldirildi — {Accepted} qabul, {Rejected} rad", events.Accepted, events.Rejected);
        }

        await TopUpDiaryAttachmentsAsync(period.Id, cancellationToken);
    }

    /// <summary>Kundaliklarga fayl biriktirish (eski bazada ham): talaba daftardagi kundalikni rasmga olib
    /// PDF qilib yuboradi — tyutor/admin profilidagi kun oynasida shu fayl ochiladi. Har 3-kundalikka bittadan,
    /// allaqachon fayli borlari o'tkazib yuboriladi.</summary>
    private async Task TopUpDiaryAttachmentsAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var entries = await db.DiaryEntries
            .Include(d => d.Attachments)
            .Where(d => d.PeriodId == periodId)
            .OrderBy(d => d.Date).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var added = 0;
        var index = 0;
        foreach (var entry in entries)
        {
            index++;
            if (entry.Attachments.Count > 0 || index % 3 != 1)
                continue;

            await AttachDiaryPdfAsync(entry, cancellationToken);
            added++;
        }

        if (added == 0)
            return;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Demo seed: {Count} ta kundalikka PDF biriktirildi", added);
    }

    /// <summary>Kundalik matnidan bir betli PDF yasab, uni saqlash xizmatiga yozadi va yozuvga biriktiradi.</summary>
    private async Task AttachDiaryPdfAsync(DiaryEntry entry, CancellationToken cancellationToken)
    {
        var date = entry.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var bytes = DemoFiles.OnePagePdf(
            $"Kundalik · {date}",
            [
                "Amaliyot kundaligi (daftardan skanerlangan nusxa).",
                string.Empty,
                ..Wrap(entry.Text, 78),
                string.Empty,
                entry.Learned is { Length: > 0 } learned ? $"O'rganganim: {learned}" : "O'rganganim: —"
            ]);

        var fileName = $"kundalik_{entry.Date:yyyy-MM-dd}.pdf";
        var file = await SaveDemoFileAsync(
            bytes, fileName, "application/pdf", StoredFileKind.DiaryAttachment,
            entry.StudentUserId, entry.SubmittedAt, pages: 1, cancellationToken);

        entry.AddAttachment(file.Id, fileName, bytes.LongLength);
    }

    /// <summary>Baytlarni saqlash xizmatiga yozib, <see cref="StoredFile"/> yozuvini qo'shadi (hali saqlanmagan).</summary>
    private async Task<StoredFile> SaveDemoFileAsync(
        byte[] bytes, string fileName, string contentType, StoredFileKind kind,
        Guid uploadedByUserId, DateTimeOffset uploadedAt, int? pages, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream(bytes, writable: false);
        var key = await storage.SaveAsync(content, fileName, contentType, cancellationToken);
        var file = StoredFile.Create(kind, fileName, contentType, bytes.LongLength, key, uploadedAt, uploadedByUserId, pages);
        db.StoredFiles.Add(file);
        return file;
    }

    /// <summary>Uzun matnni PDF satrlariga bo'lish (so'z chegarasida).</summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0)
            yield return line.ToString();
    }

    /// <summary>Davr boshidan keyingi mavjud hodisalar kaliti: (talaba, sana, tur).</summary>
    private async Task<HashSet<(Guid StudentUserId, DateOnly Date, AttendanceEventKind Kind)>> LoadExistingEventKeysAsync(
        DateOnly start, CancellationToken cancellationToken)
    {
        var rows = await db.AttendanceEvents.AsNoTracking()
            .Where(e => e.Date >= start)
            .Select(e => new { e.StudentUserId, e.Date, e.Kind })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.StudentUserId, r.Date, r.Kind)).ToHashSet();
    }

    /// <summary>Davr davomatidan hodisalar: har check-in (kech kelgan bo'lsa <c>IsLate</c>) va check-out uchun
    /// qabul qilingan urinish — nuqta korxona atrofida davomatdagi masofada (20–80 m, Rahimov uchun 410 m), aniqlik 8–25 m.
    /// Bugun (yoki oxirgi ish kuni) belgilanmagan uchta talaba — avval <see cref="SuspiciousHemisIds"/> — radius tashqarisidan
    /// (1.2–3.4 km) rad etilgan check-in urinishi qiladi: <c>GET /api/tutor/map</c> da "bad", <c>today.alerts</c> da outOfRadius.</summary>
    private async Task<(int Accepted, int Rejected)> SeedAttendanceEventsAsync(
        PracticePeriod period, DateOnly today, Random random, CancellationToken cancellationToken,
        HashSet<(Guid StudentUserId, DateOnly Date, AttendanceEventKind Kind)>? existing = null)
    {
        bool Missing(Guid studentUserId, DateOnly date, AttendanceEventKind kind)
            => existing is null || !existing.Contains((studentUserId, date, kind));

        var periodId = period.Id;
        var companyByStudent = await db.PracticeApplications.AsNoTracking()
            .Where(a => a.PeriodId == periodId && a.Status == ApplicationStatus.Approved)
            .Select(a => new { a.StudentUserId, a.Company })
            .ToDictionaryAsync(x => x.StudentUserId, x => x.Company, cancellationToken);

        var attendances = await db.DailyAttendances.AsNoTracking()
            .Where(a => a.PeriodId == periodId && a.CheckInAt != null && !a.IsManual)
            .OrderBy(a => a.Date).ThenBy(a => a.CheckInAt).ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

        var accepted = 0;
        foreach (var attendance in attendances)
        {
            if (!companyByStudent.TryGetValue(attendance.StudentUserId, out var company) || attendance.CheckInAt is not { } checkInAt)
                continue;

            if (!Missing(attendance.StudentUserId, attendance.Date, AttendanceEventKind.CheckIn))
                continue;

            var checkInDistance = attendance.CheckInDistanceM ?? 20 + random.Next(0, 60);
            var accuracy = attendance.CheckInAccuracyM ?? 8 + random.Next(0, 18);
            db.AttendanceEvents.Add(RecordAround(
                attendance.StudentUserId, company, attendance.Date, AttendanceEventKind.CheckIn, checkInAt, checkInDistance, accuracy,
                CheckInVerdict.Accept(attendance.Status == AttendanceStatus.Late), random));
            accepted++;

            if (attendance.CheckOutAt is { } checkOutAt && !attendance.AutoClosed
                && Missing(attendance.StudentUserId, attendance.Date, AttendanceEventKind.CheckOut))
            {
                db.AttendanceEvents.Add(RecordAround(
                    attendance.StudentUserId, company, attendance.Date, AttendanceEventKind.CheckOut, checkOutAt,
                    attendance.CheckOutDistanceM ?? 20 + random.Next(0, 60), 8 + random.Next(0, 18), CheckInVerdict.Accept(), random));
                accepted++;
            }
        }

        // Rad etilgan urinishlar — bugun ish kuni bo'lmasa, oxirgi ish kuni (xarita ?date= bilan ko'radi)
        var alertDay = today;
        while (alertDay > period.StartDate && !period.IsWorkDay(alertDay, isHoliday: false))
            alertDay = alertDay.AddDays(-1);

        var alertDayStudents = attendances.Where(a => a.Date == alertDay).Select(a => a.StudentUserId).ToHashSet();
        var candidateIds = companyByStudent.Keys.Where(id => !alertDayStudents.Contains(id)).ToList();
        var candidates = await db.StudentProfiles.AsNoTracking()
            .Where(p => candidateIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.HemisId })
            .ToListAsync(cancellationToken);

        var rejected = 0;
        foreach (var student in candidates
                     .OrderBy(p => Array.IndexOf(SuspiciousHemisIds, p.HemisId) is var rank and >= 0 ? rank : int.MaxValue)
                     .ThenBy(p => p.HemisId, StringComparer.Ordinal)
                     .Take(SuspiciousHemisIds.Length))
        {
            if (!Missing(student.UserId, alertDay, AttendanceEventKind.CheckIn))
                continue;

            var company = companyByStudent[student.UserId];
            var farDistance = 1_200 + random.Next(0, 2_201); // 1.2–3.4 km
            var at = At(alertDay, new TimeOnly(9, 2 + random.Next(0, 36)));
            db.AttendanceEvents.Add(RecordAround(
                student.UserId, company, alertDay, AttendanceEventKind.CheckIn, at, farDistance, 10 + random.Next(0, 21),
                CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius), random));
            rejected++;
        }

        return (accepted, rejected);
    }

    /// <summary>Korxona atrofida tasodifiy yo'nalishda <paramref name="distanceM"/> masofadagi nuqtadan urinish.
    /// <c>OccurredAt</c> (qurilma vaqti) serverga yetib kelishdan 1–3 soniya oldin.</summary>
    private static AttendanceEvent RecordAround(
        Guid studentUserId, Company company, DateOnly date, AttendanceEventKind kind, DateTimeOffset receivedAt,
        double distanceM, double accuracyM, CheckInVerdict verdict, Random random)
    {
        var point = Around(company.Location, distanceM, random.NextDouble() * 360);
        return AttendanceEvent.Record(
            studentUserId, company.Id, date, kind, receivedAt.AddSeconds(-1 - random.Next(0, 3)), receivedAt,
            point, accuracyM, company.Location.DistanceMetersTo(point), company.RadiusM, verdict);
    }

    /// <summary>Markazdan berilgan azimut va masofadagi nuqta (kichik masofalar uchun tekis yaqinlashuv, ±0.1%).</summary>
    private static GeoPoint Around(GeoPoint center, double distanceM, double bearingDegrees)
    {
        const double metersPerDegree = 111_320d;
        var bearing = bearingDegrees * Math.PI / 180d;
        var latitude = center.Latitude + distanceM * Math.Cos(bearing) / metersPerDegree;
        var longitude = center.Longitude + distanceM * Math.Sin(bearing) / (metersPerDegree * Math.Cos(center.Latitude * Math.PI / 180d));
        return new GeoPoint(latitude, longitude);
    }

    /// <summary>Toshkent kun+soat → UTC moment. Npgsql <c>timestamptz</c> ga faqat offset 0 yozadi,
    /// shuning uchun <see cref="PracticeTime.At"/> natijasi UTC'ga o'tkaziladi.</summary>
    private static DateTimeOffset At(DateOnly date, TimeOnly time) => PracticeTime.At(date, time);

    private static string DiaryText(int dayNumber, string company, string topic)
        => string.Create(CultureInfo.InvariantCulture,
            $"{dayNumber}-kun. Bugun {company} korxonasida asosiy ish {topic} bilan bog'liq bo'ldi. " +
            $"Ertalab mentor bilan kun rejasini kelishib oldik, keyin topshiriqlarni bosqichma-bosqich bajardim. " +
            $"Kun davomida uchragan qiyinchiliklarni jamoa bilan muhokama qilib, yechimlarni hujjatlashtirdim. " +
            $"Kun yakunida natijalarni rahbarga taqdim etdim va ertangi kun uchun vazifalar ro'yxatini tuzdim.");

    private sealed class DemoStudent(User user, StudentProfile profile, Company company, int index)
    {
        public User User { get; } = user;
        public StudentProfile Profile { get; } = profile;
        public Company Company { get; } = company;
        public int Index { get; } = index;
        public PracticeApplication? Application { get; set; }
    }
}

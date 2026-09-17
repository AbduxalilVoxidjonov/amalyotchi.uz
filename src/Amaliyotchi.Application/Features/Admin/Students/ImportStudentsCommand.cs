using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>POST /api/admin/students/import</c> (multipart, <c>file</c>) → 200 <see cref="StudentImportResult"/>.
/// Shablon bo'yicha to'ldirilgan <c>.xlsx</c> dan talabalarni ommaviy qo'shadi. Xato qatorlar tashlab yuboriladi
/// va hisobotda ko'rsatiladi, TO'G'RILARI saqlanadi (qisman import — FUNKSIONAL-QOLLANMA T2).
/// Fayl o'qilmasa yoki sarlavha qatori topilmasa → 400.</summary>
public sealed record ImportStudentsCommand(UploadedFile? File) : IRequest<StudentImportResult>;

/// <summary>Qator xatolari matni — hisobotda foydalanuvchiga shu ko'rinishda chiqadi.</summary>
public static class StudentImportMessages
{
    public const string FullNameRequiredMessage = "FISH bo'sh.";
    public const string FullNameLengthMessage = "FISH 200 ta belgidan oshmasligi kerak.";
    public const string HemisRequiredMessage = "HEMIS ID bo'sh.";
    public const string HemisFormatMessage = "HEMIS ID 5–20 ta raqamdan iborat bo'lishi kerak.";
    public const string HemisDuplicateInFileMessage = "Bu HEMIS ID faylda takrorlanmoqda.";
    public const string HemisTakenMessage = "Bu HEMIS ID bilan talaba allaqachon bor.";
    public const string GroupRequiredMessage = "Guruh bo'sh.";
    public const string GroupNotFoundMessage = "Bunday faol guruh yo'q — shablonning «Guruhlar» varag'idan tanlang.";
    public const string PhoneFormatMessage = "Telefon raqami noto'g'ri. Namuna: +998901234567";
    public const string PhoneDuplicateInFileMessage = "Bu telefon raqami faylda takrorlanmoqda.";
    public const string PhoneTakenMessage = "Bu telefon raqami bilan foydalanuvchi bor.";
}

internal sealed class ImportStudentsCommandHandler(
    IApplicationDbContext db, IStudentImportExcel excel, IAuditWriter audit)
    : IRequestHandler<ImportStudentsCommand, StudentImportResult>
{
    private const int FullNameMaxLength = 200;

    public async Task<StudentImportResult> Handle(ImportStudentsCommand request, CancellationToken cancellationToken)
    {
        // Validator allaqachon tekshirgan; bu — nullable kontrakt uchun himoya tarmog'i.
        var file = request.File ?? throw new DomainException(ImportStudentsCommandValidator.RequiredMessage);

        IReadOnlyList<StudentImportRow> rows;
        await using (var stream = file.OpenRead())
            rows = excel.Read(stream, StudentImportLimits.MaxRows);

        var groups = await StudentImportGroups.LoadAsync(db, cancellationToken);
        var byName = groups
            .GroupBy(g => StudentImportGroups.Key(g.Name))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // Takrorlanish nazorati: bazadagi qiymatlar (unikal indekslar bilan bir xil shart) + shu fayl ichidagilar.
        var takenHemis = await db.StudentProfiles.AsNoTracking()
            .Select(p => p.HemisId).ToListAsync(cancellationToken);
        var takenPhones = await db.Users.AsNoTracking()
            .Where(u => u.PhoneNumber != null).Select(u => u.PhoneNumber!).ToListAsync(cancellationToken);

        var hemisSeen = new HashSet<string>(takenHemis, StringComparer.Ordinal);
        var phoneSeen = new HashSet<string>(takenPhones, StringComparer.Ordinal);
        var fileHemis = new HashSet<string>(StringComparer.Ordinal);
        var filePhones = new HashSet<string>(StringComparer.Ordinal);

        var errors = new List<StudentImportError>();
        var created = 0;

        foreach (var row in rows)
        {
            var before = errors.Count;

            var fullName = row.FullName?.Trim();
            if (string.IsNullOrEmpty(fullName))
                errors.Add(Error(row, StudentImportColumns.FullName, row.FullName, StudentImportMessages.FullNameRequiredMessage));
            else if (fullName.Length > FullNameMaxLength)
                errors.Add(Error(row, StudentImportColumns.FullName, fullName, StudentImportMessages.FullNameLengthMessage));

            var hemisId = string.Empty;
            if (string.IsNullOrWhiteSpace(row.HemisId))
                errors.Add(Error(row, StudentImportColumns.HemisId, row.HemisId, StudentImportMessages.HemisRequiredMessage));
            else if (!Hemis.TryNormalize(row.HemisId, out hemisId))
                errors.Add(Error(row, StudentImportColumns.HemisId, row.HemisId, StudentImportMessages.HemisFormatMessage));
            else if (fileHemis.Contains(hemisId))
                errors.Add(Error(row, StudentImportColumns.HemisId, row.HemisId, StudentImportMessages.HemisDuplicateInFileMessage));
            else if (hemisSeen.Contains(hemisId))
                errors.Add(Error(row, StudentImportColumns.HemisId, row.HemisId, StudentImportMessages.HemisTakenMessage));

            ImportGroup? group = null;
            if (string.IsNullOrWhiteSpace(row.Group))
            {
                errors.Add(Error(row, StudentImportColumns.Group, row.Group, StudentImportMessages.GroupRequiredMessage));
            }
            else if (!byName.TryGetValue(StudentImportGroups.Key(row.Group), out var matches))
            {
                errors.Add(Error(row, StudentImportColumns.Group, row.Group, StudentImportMessages.GroupNotFoundMessage));
            }
            else if (matches.Count > 1)
            {
                errors.Add(Error(row, StudentImportColumns.Group, row.Group, AmbiguousGroupMessage(matches)));
            }
            else
            {
                group = matches[0];
            }

            string? phone = null;
            if (!string.IsNullOrWhiteSpace(row.Phone))
            {
                if (!Phone.TryNormalize(row.Phone, out var normalized))
                    errors.Add(Error(row, StudentImportColumns.Phone, row.Phone, StudentImportMessages.PhoneFormatMessage));
                else if (filePhones.Contains(normalized))
                    errors.Add(Error(row, StudentImportColumns.Phone, row.Phone, StudentImportMessages.PhoneDuplicateInFileMessage));
                else if (phoneSeen.Contains(normalized))
                    errors.Add(Error(row, StudentImportColumns.Phone, row.Phone, StudentImportMessages.PhoneTakenMessage));
                else
                    phone = normalized;
            }

            if (errors.Count != before || group is null || fullName is null)
                continue;

            var user = User.CreateStudent(fullName, group.FacultyId, phone);
            db.Users.Add(user);
            db.StudentProfiles.Add(StudentProfile.Create(user.Id, hemisId, group.GroupId));

            fileHemis.Add(hemisId);
            if (phone is not null)
                filePhones.Add(phone);
            created++;
        }

        if (created > 0)
        {
            await audit.WriteAsync(
                AuditAction.StudentsImported, nameof(StudentProfile),
                changes: JsonSerializer.Serialize(new
                {
                    fileName = file.FileName,
                    total = rows.Count,
                    created,
                    failed = rows.Count - created
                }),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        return new StudentImportResult(rows.Count, created, rows.Count - created, errors);
    }

    private static StudentImportError Error(StudentImportRow row, string column, string? value, string message)
        => new(row.RowNumber, column, value, message);

    /// <summary>Bir xil nomli guruh bir nechta yo'nalishda bo'lsa — qaysi biri ekani noaniq, qator rad etiladi.</summary>
    private static string AmbiguousGroupMessage(IReadOnlyList<ImportGroup> matches)
        => $"Bu nomli guruh bir nechta yo'nalishda bor ({string.Join(", ", matches.Select(m => m.Direction))}) — nomini aniqlashtiring.";
}

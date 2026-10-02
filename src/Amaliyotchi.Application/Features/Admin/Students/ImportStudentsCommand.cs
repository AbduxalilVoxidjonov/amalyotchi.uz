using System.Text.Json;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>POST /api/admin/students/import</c> (multipart, <c>file</c>) → 200 <see cref="ImportResult"/>.
/// Shablon bo'yicha to'ldirilgan <c>.xlsx</c> dan talabalarni ommaviy qo'shadi. Xato qatorlar tashlab yuboriladi
/// va hisobotda ko'rsatiladi, TO'G'RILARI saqlanadi (qisman import — FUNKSIONAL-QOLLANMA T2).
/// Fayl o'qilmasa yoki sarlavha qatori topilmasa → 400.</summary>
public sealed record ImportStudentsCommand(UploadedFile? File) : IRequest<ImportResult>;

/// <summary>Qator xatolari matni — hisobotda foydalanuvchiga shu ko'rinishda chiqadi. Maydon xabarlari
/// (<see cref="StudentFieldRules"/>) yakka talaba formasida (<c>POST /api/admin/students</c>) ham ishlatiladi.</summary>
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
    : IRequestHandler<ImportStudentsCommand, ImportResult>
{
    public async Task<ImportResult> Handle(ImportStudentsCommand request, CancellationToken cancellationToken)
    {
        // Validator allaqachon tekshirgan; bu — nullable kontrakt uchun himoya tarmog'i.
        var file = request.File ?? throw new DomainException(ImportStudentsCommandValidator.RequiredMessage);

        IReadOnlyList<StudentImportRow> rows;
        await using (var stream = file.OpenRead())
            rows = excel.Read(stream, ExcelImport.MaxRows);

        var groups = await StudentImportGroups.LoadAsync(db, cancellationToken);
        var byName = groups
            .GroupBy(g => StudentImportGroups.Key(g.Name))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // Takrorlanish nazorati: bazadagi qiymatlar (unikal indekslar bilan bir xil shart) + shu fayl ichidagilar.
        var takenHemis = await StudentRegistration.TakenHemisIds(db).ToListAsync(cancellationToken);
        var takenPhones = await StudentRegistration.TakenPhones(db).ToListAsync(cancellationToken);

        var hemisSeen = new HashSet<string>(takenHemis, StringComparer.Ordinal);
        var phoneSeen = new HashSet<string>(takenPhones, StringComparer.Ordinal);
        var fileHemis = new HashSet<string>(StringComparer.Ordinal);
        var filePhones = new HashSet<string>(StringComparer.Ordinal);

        var errors = new List<ImportError>();
        var created = 0;

        foreach (var row in rows)
        {
            var before = errors.Count;

            if (StudentFieldRules.FullNameError(row.FullName, out var fullName) is { } fullNameError)
                errors.Add(Error(row, StudentImportColumns.FullName,
                    fullName.Length == 0 ? row.FullName : fullName, fullNameError));

            if (StudentFieldRules.HemisIdError(row.HemisId, out var hemisId) is { } hemisError)
                errors.Add(Error(row, StudentImportColumns.HemisId, row.HemisId, hemisError));
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

            if (StudentFieldRules.PhoneError(row.Phone, out var phone) is { } phoneError)
                errors.Add(Error(row, StudentImportColumns.Phone, row.Phone, phoneError));
            else if (phone is not null && filePhones.Contains(phone))
                errors.Add(Error(row, StudentImportColumns.Phone, row.Phone, StudentImportMessages.PhoneDuplicateInFileMessage));
            else if (phone is not null && phoneSeen.Contains(phone))
                errors.Add(Error(row, StudentImportColumns.Phone, row.Phone, StudentImportMessages.PhoneTakenMessage));

            if (errors.Count != before || group is null)
                continue;

            StudentRegistration.Add(db, fullName, hemisId, group, phone);

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

        return new ImportResult(rows.Count, created, rows.Count - created, errors);
    }

    private static ImportError Error(StudentImportRow row, string column, string? value, string message)
        => new(row.RowNumber, column, value, message);

    /// <summary>Bir xil nomli guruh bir nechta yo'nalishda bo'lsa — qaysi biri ekani noaniq, qator rad etiladi.</summary>
    private static string AmbiguousGroupMessage(IReadOnlyList<ImportGroup> matches)
        => $"Bu nomli guruh bir nechta yo'nalishda bor ({string.Join(", ", matches.Select(m => m.Direction))}) — nomini aniqlashtiring.";
}

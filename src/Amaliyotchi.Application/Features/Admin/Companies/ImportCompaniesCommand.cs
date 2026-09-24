using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;
using Tin = Amaliyotchi.Domain.ValueObjects.Tin;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary><c>GET /api/admin/companies/import/template</c> → to'ldirish uchun <c>.xlsx</c> shablon.</summary>
public sealed record GetCompanyImportTemplateQuery : IRequest<byte[]>;

internal sealed class GetCompanyImportTemplateQueryHandler(ICompanyImportExcel excel)
    : IRequestHandler<GetCompanyImportTemplateQuery, byte[]>
{
    public Task<byte[]> Handle(GetCompanyImportTemplateQuery request, CancellationToken cancellationToken)
        => Task.FromResult(excel.BuildTemplate());
}

/// <summary><c>POST /api/admin/companies/import</c> (multipart, <c>file</c>) → 200 <see cref="ImportResult"/>.
/// Bir vaqtda ko'p korxonani shablon orqali yuklash. Xato qatorlar tashlab yuboriladi va hisobotda
/// ko'rsatiladi, to'g'rilari saqlanadi (qisman import — talabalar importi bilan bir xil qoida).</summary>
public sealed record ImportCompaniesCommand(UploadedFile? File) : IRequest<ImportResult>;

/// <summary>Qator xatolari matni — hisobotda foydalanuvchiga shu ko'rinishda chiqadi.</summary>
public static class CompanyImportMessages
{
    public const string NameRequiredMessage = "Korxona nomi bo'sh.";
    public const string TinRequiredMessage = "STIR bo'sh.";
    public const string TinFormatMessage = "STIR 9 ta raqamdan iborat bo'lishi kerak.";
    public const string TinDuplicateInFileMessage = "Bu STIR faylda takrorlanmoqda.";
    public const string TinTakenMessage = "Bu STIR bilan korxona allaqachon bor.";
    public const string ActivityRequiredMessage = "Faoliyat turi bo'sh.";
    public const string AddressRequiredMessage = "Manzil bo'sh.";
    public const string LatRequiredMessage = "Kenglik (lat) bo'sh.";
    public const string LatFormatMessage = "Kenglik (lat) -90 va 90 oralig'idagi son bo'lishi kerak. Namuna: 41.31105";
    public const string LngRequiredMessage = "Uzunlik (lng) bo'sh.";
    public const string LngFormatMessage = "Uzunlik (lng) -180 va 180 oralig'idagi son bo'lishi kerak. Namuna: 69.27972";
    public const string SupervisorRequiredMessage = "Rahbar FISH bo'sh.";
    public const string SupervisorPhoneRequiredMessage = "Rahbar telefoni bo'sh.";
    public const string PhoneFormatMessage = "Telefon raqami noto'g'ri. Namuna: +998901234567";

    public static readonly string RadiusFormatMessage =
        $"Radius {Company.MinRadiusM}–{Company.MaxRadiusM} m oralig'idagi butun son bo'lishi kerak.";

    public static string TooLongMessage(string label, int maxLength) =>
        $"{label} {maxLength} belgidan oshmasligi kerak.";
}

internal sealed class ImportCompaniesCommandHandler(
    IApplicationDbContext db, ICompanyImportExcel excel, IAuditWriter audit, IClock clock)
    : IRequestHandler<ImportCompaniesCommand, ImportResult>
{
    public async Task<ImportResult> Handle(ImportCompaniesCommand request, CancellationToken cancellationToken)
    {
        var file = request.File ?? throw new DomainException(ImportCompaniesCommandValidator.RequiredMessage);

        IReadOnlyList<CompanyImportRow> rows;
        await using (var stream = file.OpenRead())
            rows = excel.Read(stream, ExcelImport.MaxRows);

        var defaultRadius = await CompanyWrite.ResolveRadiusAsync(db, null, cancellationToken);
        var takenTins = await db.Companies.AsNoTracking().Select(c => c.Tin).ToListAsync(cancellationToken);
        var tinSeen = new HashSet<string>(takenTins, StringComparer.Ordinal);
        var fileTins = new HashSet<string>(StringComparer.Ordinal);

        var errors = new List<ImportError>();
        var created = 0;

        foreach (var row in rows)
        {
            var before = errors.Count;

            var name = Required(row, row.Name, CompanyImportColumns.Name,
                CompanyImportMessages.NameRequiredMessage, Company.NameMaxLength, errors);
            var activity = Required(row, row.Activity, CompanyImportColumns.Activity,
                CompanyImportMessages.ActivityRequiredMessage, Company.ActivityMaxLength, errors);
            var address = Required(row, row.Address, CompanyImportColumns.Address,
                CompanyImportMessages.AddressRequiredMessage, Company.AddressMaxLength, errors);
            var supervisor = Required(row, row.SupervisorName, CompanyImportColumns.SupervisorName,
                CompanyImportMessages.SupervisorRequiredMessage, Company.NameMaxLength, errors);

            var tin = string.Empty;
            if (string.IsNullOrWhiteSpace(row.Tin))
                errors.Add(Error(row, CompanyImportColumns.Tin, row.Tin, CompanyImportMessages.TinRequiredMessage));
            else if (!Tin.TryNormalize(row.Tin, out tin))
                errors.Add(Error(row, CompanyImportColumns.Tin, row.Tin, CompanyImportMessages.TinFormatMessage));
            else if (fileTins.Contains(tin))
                errors.Add(Error(row, CompanyImportColumns.Tin, row.Tin, CompanyImportMessages.TinDuplicateInFileMessage));
            else if (tinSeen.Contains(tin))
                errors.Add(Error(row, CompanyImportColumns.Tin, row.Tin, CompanyImportMessages.TinTakenMessage));

            var lat = Coordinate(row, row.Lat, CompanyImportColumns.Lat, -90, 90,
                CompanyImportMessages.LatRequiredMessage, CompanyImportMessages.LatFormatMessage, errors);
            var lng = Coordinate(row, row.Lng, CompanyImportColumns.Lng, -180, 180,
                CompanyImportMessages.LngRequiredMessage, CompanyImportMessages.LngFormatMessage, errors);

            var radiusM = defaultRadius;
            if (!string.IsNullOrWhiteSpace(row.Radius))
            {
                if (!int.TryParse(row.Radius.Trim(), out var parsed)
                    || parsed is < Company.MinRadiusM or > Company.MaxRadiusM)
                {
                    errors.Add(Error(row, CompanyImportColumns.Radius, row.Radius, CompanyImportMessages.RadiusFormatMessage));
                }
                else
                {
                    radiusM = parsed;
                }
            }

            var supervisorPhone = string.Empty;
            if (string.IsNullOrWhiteSpace(row.SupervisorPhone))
                errors.Add(Error(row, CompanyImportColumns.SupervisorPhone, row.SupervisorPhone, CompanyImportMessages.SupervisorPhoneRequiredMessage));
            else if (!Phone.TryNormalize(row.SupervisorPhone, out supervisorPhone))
                errors.Add(Error(row, CompanyImportColumns.SupervisorPhone, row.SupervisorPhone, CompanyImportMessages.PhoneFormatMessage));

            string? mentorPhone = null;
            if (!string.IsNullOrWhiteSpace(row.MentorPhone))
            {
                if (!Phone.TryNormalize(row.MentorPhone, out var normalized))
                    errors.Add(Error(row, CompanyImportColumns.MentorPhone, row.MentorPhone, CompanyImportMessages.PhoneFormatMessage));
                else
                    mentorPhone = normalized;
            }

            if (errors.Count != before || name is null || activity is null || address is null
                || supervisor is null || lat is null || lng is null)
            {
                continue;
            }

            var company = Company.Create(
                name, tin, activity, address, new GeoPoint(lat.Value, lng.Value), radiusM,
                supervisor, supervisorPhone, row.MentorName, mentorPhone, clock.UtcNow);

            db.Companies.Add(company);
            fileTins.Add(tin);
            created++;
        }

        if (created > 0)
        {
            await audit.WriteAsync(
                AuditAction.CompaniesImported, nameof(Company),
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

    private static string? Required(
        CompanyImportRow row, string? value, string column, string requiredMessage, int maxLength, List<ImportError> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(Error(row, column, value, requiredMessage));
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            errors.Add(Error(row, column, value, CompanyImportMessages.TooLongMessage(column, maxLength)));
            return null;
        }

        return trimmed;
    }

    private static double? Coordinate(
        CompanyImportRow row, string? value, string column, double min, double max,
        string requiredMessage, string formatMessage, List<ImportError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(Error(row, column, value, requiredMessage));
            return null;
        }

        // Excel kasr ajratgichi vergul bo'lishi mumkin ("41,31105").
        var cleaned = value.Trim().Replace(',', '.');
        if (!double.TryParse(cleaned, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || double.IsNaN(parsed) || parsed < min || parsed > max)
        {
            errors.Add(Error(row, column, value, formatMessage));
            return null;
        }

        return parsed;
    }

    private static ImportError Error(CompanyImportRow row, string column, string? value, string message)
        => new(row.RowNumber, column, value, message);
}

/// <summary>Faylning o'zini tekshiradi (mazmuni emas — u handler'da qator-baqator tekshiriladi).</summary>
public sealed class ImportCompaniesCommandValidator : AbstractValidator<ImportCompaniesCommand>
{
    public const string RequiredMessage = "Excel fayl tanlanmagan.";
    public const string EmptyMessage = "Fayl bo'sh.";
    public static readonly string TooLargeMessage =
        $"Fayl hajmi {ExcelImport.MaxFileBytes / (1024 * 1024)} MB dan oshmasligi kerak.";
    public static readonly string ExtensionMessage =
        $"Faqat {ExcelImport.FileExtension} fayl qabul qilinadi (eski .xls formati emas).";

    public ImportCompaniesCommandValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage(RequiredMessage);

        RuleFor(x => x.File!.Length)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(EmptyMessage)
            .LessThanOrEqualTo(ExcelImport.MaxFileBytes).WithMessage(TooLargeMessage)
            .When(x => x.File is not null);

        RuleFor(x => x.File!.FileName)
            .Must(name => name is not null
                && name.EndsWith(ExcelImport.FileExtension, StringComparison.OrdinalIgnoreCase))
            .WithMessage(ExtensionMessage)
            .When(x => x.File is not null);
    }
}

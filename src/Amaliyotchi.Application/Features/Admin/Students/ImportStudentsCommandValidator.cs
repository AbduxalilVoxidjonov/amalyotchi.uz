using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Faylning o'zini tekshiradi (mazmuni emas — u handler'da qator-baqator tekshiriladi).</summary>
public sealed class ImportStudentsCommandValidator : AbstractValidator<ImportStudentsCommand>
{
    public const string RequiredMessage = "Excel fayl tanlanmagan.";
    public const string EmptyMessage = "Fayl bo'sh.";
    public static readonly string TooLargeMessage =
        $"Fayl hajmi {StudentImportLimits.MaxFileBytes / (1024 * 1024)} MB dan oshmasligi kerak.";
    public static readonly string ExtensionMessage =
        $"Faqat {StudentImportLimits.FileExtension} fayl qabul qilinadi (eski .xls formati emas).";

    public ImportStudentsCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage(RequiredMessage);

        RuleFor(x => x.File!.Length)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(EmptyMessage)
            .LessThanOrEqualTo(StudentImportLimits.MaxFileBytes).WithMessage(TooLargeMessage)
            .When(x => x.File is not null);

        RuleFor(x => x.File!.FileName)
            .Must(name => name is not null
                && name.EndsWith(StudentImportLimits.FileExtension, StringComparison.OrdinalIgnoreCase))
            .WithMessage(ExtensionMessage)
            .When(x => x.File is not null);
    }
}

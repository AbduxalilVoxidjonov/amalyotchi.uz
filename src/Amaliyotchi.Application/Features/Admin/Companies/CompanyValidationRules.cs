using Amaliyotchi.Domain.Companies;
using FluentValidation;
using Phone = Amaliyotchi.Domain.ValueObjects.PhoneNumber;
using Tin = Amaliyotchi.Domain.ValueObjects.Tin;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary>Create/Update buyruqlari uchun umumiy validatsiya qoidalari.
/// Domain ham shu shartlarni tekshiradi — bu yerda maqsad maydonga bog'langan (400 `errors`) xabar berish.</summary>
internal static class CompanyValidationRules
{
    public const string NameRequiredMessage = "Korxona nomini kiriting.";
    public const string TinRequiredMessage = "STIR ni kiriting.";
    public const string TinFormatMessage = "STIR 9 ta raqamdan iborat bo'lishi kerak. Namuna: 123456789";
    public const string ActivityRequiredMessage = "Faoliyat turini kiriting.";
    public const string AddressRequiredMessage = "Manzilni kiriting.";
    public const string LatMessage = "Kenglik (lat) -90 va 90 oralig'ida bo'lishi kerak.";
    public const string LngMessage = "Uzunlik (lng) -180 va 180 oralig'ida bo'lishi kerak.";
    public const string SupervisorRequiredMessage = "Rahbar FISH ni kiriting.";
    public const string PhoneFormatMessage = "Telefon raqami noto'g'ri. Namuna: +998901234567";

    public static readonly string RadiusMessage =
        $"Radius {Company.MinRadiusM}–{Company.MaxRadiusM} m oralig'ida bo'lishi kerak.";

    public static bool IsValidTin(string? tin) => Tin.TryNormalize(tin, out _);

    public static bool IsValidPhone(string? phone) => Phone.TryNormalize(phone, out _);

    public static bool IsValidOptionalPhone(string? phone)
        => string.IsNullOrWhiteSpace(phone) || IsValidPhone(phone);

    public static bool IsValidRadius(int? radiusM)
        => radiusM is null || radiusM is >= Company.MinRadiusM and <= Company.MaxRadiusM;

    /// <summary>Create va Update validatorlari bir xil maydon qoidalarini ishlatadi.</summary>
    public static void Fields<T>(AbstractValidator<T> validator, Func<T, CompanyFields> fields)
    {
        validator.RuleFor(x => fields(x).Name)
            .NotEmpty().WithMessage(NameRequiredMessage)
            .MaximumLength(Company.NameMaxLength).WithMessage($"Korxona nomi {Company.NameMaxLength} belgidan oshmasligi kerak.")
            .OverridePropertyName("name");

        validator.RuleFor(x => fields(x).Tin)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(TinRequiredMessage)
            .Must(IsValidTin).WithMessage(TinFormatMessage)
            .OverridePropertyName("tin");

        validator.RuleFor(x => fields(x).Activity)
            .NotEmpty().WithMessage(ActivityRequiredMessage)
            .MaximumLength(Company.ActivityMaxLength).WithMessage($"Faoliyat turi {Company.ActivityMaxLength} belgidan oshmasligi kerak.")
            .OverridePropertyName("activity");

        validator.RuleFor(x => fields(x).Address)
            .NotEmpty().WithMessage(AddressRequiredMessage)
            .MaximumLength(Company.AddressMaxLength).WithMessage($"Manzil {Company.AddressMaxLength} belgidan oshmasligi kerak.")
            .OverridePropertyName("address");

        validator.RuleFor(x => fields(x).Lat)
            .InclusiveBetween(-90, 90).WithMessage(LatMessage)
            .OverridePropertyName("lat");

        validator.RuleFor(x => fields(x).Lng)
            .InclusiveBetween(-180, 180).WithMessage(LngMessage)
            .OverridePropertyName("lng");

        validator.RuleFor(x => fields(x).RadiusM)
            .Must(IsValidRadius).WithMessage(RadiusMessage)
            .OverridePropertyName("radiusM");

        validator.RuleFor(x => fields(x).SupervisorName)
            .NotEmpty().WithMessage(SupervisorRequiredMessage)
            .MaximumLength(Company.NameMaxLength).WithMessage($"Rahbar FISH {Company.NameMaxLength} belgidan oshmasligi kerak.")
            .OverridePropertyName("supervisorName");

        validator.RuleFor(x => fields(x).SupervisorPhone)
            .Must(IsValidPhone).WithMessage(PhoneFormatMessage)
            .OverridePropertyName("supervisorPhone");

        validator.RuleFor(x => fields(x).MentorPhone)
            .Must(IsValidOptionalPhone).WithMessage(PhoneFormatMessage)
            .OverridePropertyName("mentorPhone");
    }
}

/// <summary>Validator uchun maydonlar ko'rinishi (Create va Update bir xil to'plamga ega).</summary>
internal sealed record CompanyFields(
    string Name,
    string Tin,
    string Activity,
    string Address,
    double Lat,
    double Lng,
    int? RadiusM,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorPhone);

using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Settings;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Settings;

/// <summary>Har kalit alohida xato beradi (<c>errors.geofenceRadius</c>) — domain qoidasi <see cref="SettingDefinition.Validate"/> da,
/// bu yerda faqat noma'lum kalit va bo'sh so'rov tekshiriladi.</summary>
public sealed class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        RuleFor(x => x.Values)
            .NotEmpty().WithMessage("Kamida bitta sozlama yuborilishi kerak.");

        RuleFor(x => x.Values)
            .Custom((values, context) =>
            {
                if (values is null)
                    return;

                foreach (var (key, value) in values)
                {
                    if (!SettingKeys.TryGet(key, out var definition))
                    {
                        context.AddFailure(key, $"Noma'lum sozlama kaliti: '{key}'.");
                        continue;
                    }

                    try
                    {
                        definition.Validate(value);
                    }
                    catch (DomainException ex)
                    {
                        context.AddFailure(key, ex.Message);
                    }
                }
            });
    }
}

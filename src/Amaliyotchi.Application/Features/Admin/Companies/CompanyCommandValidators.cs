using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Companies;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
        => CompanyValidationRules.Fields(this, x => new CompanyFields(
            x.Name, x.Tin, x.Activity, x.Address, x.Lat, x.Lng, x.RadiusM,
            x.SupervisorName, x.SupervisorPhone, x.MentorPhone));
}

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Korxona ko'rsatilmagan.");
        CompanyValidationRules.Fields(this, x => new CompanyFields(
            x.Name, x.Tin, x.Activity, x.Address, x.Lat, x.Lng, x.RadiusM,
            x.SupervisorName, x.SupervisorPhone, x.MentorPhone));
    }
}

public sealed class SetCompanyStatusCommandValidator : AbstractValidator<SetCompanyStatusCommand>
{
    public SetCompanyStatusCommandValidator()
        => RuleFor(x => x.Id).NotEmpty().WithMessage("Korxona ko'rsatilmagan.");
}

public sealed class DeleteCompanyCommandValidator : AbstractValidator<DeleteCompanyCommand>
{
    public DeleteCompanyCommandValidator()
        => RuleFor(x => x.Id).NotEmpty().WithMessage("Korxona ko'rsatilmagan.");
}

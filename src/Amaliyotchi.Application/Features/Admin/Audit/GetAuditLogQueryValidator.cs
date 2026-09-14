using Amaliyotchi.Application.Features.Admin.Common;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Audit;

public sealed class GetAuditLogQueryValidator : PagedQueryValidator<GetAuditLogQuery>
{
    public GetAuditLogQueryValidator()
    {
        RuleFor(x => x.Action)
            .IsInEnum().WithMessage("Noma'lum amal turi.")
            .When(x => x.Action is not null);
    }
}

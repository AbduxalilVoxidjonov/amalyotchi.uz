using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Organization;
using FluentValidation;

namespace Amaliyotchi.Application.Features.Admin.Students;

public sealed class GetAdminStudentsQueryValidator : PagedQueryValidator<GetAdminStudentsQuery>
{
    public GetAdminStudentsQueryValidator()
    {
        RuleFor(x => x.Course)
            .InclusiveBetween(StudentGroup.MinCourse, StudentGroup.MaxCourse)
            .WithMessage($"Kurs {StudentGroup.MinCourse}–{StudentGroup.MaxCourse} oralig'ida bo'lishi kerak.")
            .When(x => x.Course is not null);
    }
}

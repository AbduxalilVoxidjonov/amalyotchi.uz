using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using ValidationException = Amaliyotchi.Application.Common.Exceptions.ValidationException;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>POST /api/admin/students</c>: <c>{ fullName, hemisId, groupId, phoneNumber? }</c> → 201 <see cref="StudentRow"/>
/// (ro'yxat qatori bilan bir xil shakl). Talaba Excel import bilan AYNAN bir xil yaratiladi
/// (<see cref="StudentFieldRules"/>, <see cref="StudentRegistration"/>): maydon qoidalari va xabarlari, faol guruh to'plami,
/// HEMIS ID/telefon bandligi (o'chirilmaganlar orasida). Maydon xatolari → 400 <c>errors.fullName|hemisId|groupId|phoneNumber</c>
/// (camelCase); guruh topilmasa yoki faol emas → 400 <c>errors.groupId</c>; HEMIS ID yoki telefon band → 409.</summary>
public sealed record CreateStudentCommand(string? FullName, string? HemisId, Guid? GroupId, string? PhoneNumber)
    : IRequest<StudentRow>;

/// <summary>Yakka yaratishga xos xabarlar (maydon xabarlari — <see cref="StudentImportMessages"/>, import bilan umumiy).</summary>
public static class StudentCreateMessages
{
    public const string GroupNotFoundMessage = "Bunday faol guruh yo'q.";
    public const string HemisTakenMessage = "Bu HEMIS ID bilan talaba allaqachon mavjud.";
}

/// <summary>400 <c>errors</c> kalitlari — so'rov tanasidagi (camelCase) maydon nomlari bilan bir xil.</summary>
internal static class CreateStudentFields
{
    public const string FullName = "fullName";
    public const string HemisId = "hemisId";
    public const string GroupId = "groupId";
    public const string PhoneNumber = "phoneNumber";
}

internal sealed class CreateStudentCommandHandler(IApplicationDbContext db, IClock clock, IAuditWriter audit)
    : IRequestHandler<CreateStudentCommand, StudentRow>
{
    public async Task<StudentRow> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        // Validator allaqachon tekshirgan — bu yerda faqat normallashgan qiymatlar olinadi.
        StudentFieldRules.FullNameError(request.FullName, out var fullName);
        StudentFieldRules.HemisIdError(request.HemisId, out var hemisId);
        StudentFieldRules.PhoneError(request.PhoneNumber, out var phone);

        var group = await StudentImportGroups.FindAsync(db, request.GroupId!.Value, cancellationToken)
            ?? throw new ValidationException(
                new Dictionary<string, string[]> { [CreateStudentFields.GroupId] = [StudentCreateMessages.GroupNotFoundMessage] },
                StudentCreateMessages.GroupNotFoundMessage);

        if (await StudentRegistration.IsHemisTakenAsync(db, hemisId, cancellationToken))
            throw new ConflictException(StudentCreateMessages.HemisTakenMessage);
        if (phone is not null && await StudentRegistration.IsPhoneTakenAsync(db, phone, cancellationToken))
            throw new ConflictException(StudentImportMessages.PhoneTakenMessage);

        var user = StudentRegistration.Add(db, fullName, hemisId, group, phone);

        await audit.WriteAsync(
            AuditAction.StudentCreated, nameof(User), user.Id.ToString(),
            changes: JsonSerializer.Serialize(new { hemisId, groupId = group.GroupId }),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return await GetAdminStudentsQueryHandler.LoadRowAsync(db, clock, user.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), user.Id);
    }
}

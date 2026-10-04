using System.Text.Json;
using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Student.CheckIn;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Domain.Files;
using FluentValidation;
using MediatR;
using ValidationException = Amaliyotchi.Application.Common.Exceptions.ValidationException;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Face;

/// <summary><c>GET /api/student/face</c> — o'z etalon yuz rasmi holati va sozlama (<c>required</c>).</summary>
public sealed record GetStudentFaceQuery : IRequest<StudentFaceDto>;

internal sealed class GetStudentFaceQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetStudentFaceQuery, StudentFaceDto>
{
    public async Task<StudentFaceDto> Handle(GetStudentFaceQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);
        var enrollment = await db.StudentFaceEnrollments.AsNoTracking()
            .FirstOrDefaultAsync(f => f.StudentUserId == userId, cancellationToken);
        return StudentFaceDto.From(enrollment, settings.FaceVerificationEnabled);
    }
}

/// <summary><c>POST /api/student/face</c> (multipart: <c>photo</c>, <c>consent=true</c>) → 200 <see cref="StudentFaceDto"/>
/// (holat <c>pending</c>). Ruxsat: none/pending/rejected (pending almashtiriladi); approved → 409.
/// Rasmda aynan bitta yuz bo'lishi shart (400, <c>errors.Photo</c>). Modellar yo'q → 503.
/// Audit: <see cref="AuditAction.FaceEnrollmentSubmitted"/>.</summary>
public sealed record SubmitStudentFaceCommand(UploadedFile? Photo, bool Consent) : IRequest<StudentFaceDto>;

/// <summary>Rozilik va rasm qoidalari (check-in selfisi bilan bir xil: ≤ 5 MB, JPEG/PNG/WebP/HEIC) — <c>errors.Consent</c>,
/// <c>errors.Photo</c>.</summary>
public sealed class SubmitStudentFaceCommandValidator : AbstractValidator<SubmitStudentFaceCommand>
{
    public const string ConsentRequiredMessage = "Rozilik berilishi kerak.";
    public const string PhotoRequiredMessage = "Yuz rasmi majburiy.";

    public SubmitStudentFaceCommandValidator()
    {
        RuleFor(x => x.Consent)
            .Equal(true).WithMessage(ConsentRequiredMessage);

        RuleFor(x => x.Photo)
            .NotNull().WithMessage(PhotoRequiredMessage);

        When(x => x.Photo is not null, () =>
        {
            RuleFor(x => x.Photo!)
                .Must(p => p.Length > 0 && p.Length <= GeoRequestValidator<CheckInCommand>.MaxPhotoSizeBytes)
                .WithMessage("Rasm 5 MB dan oshmasligi va bo'sh bo'lmasligi kerak.")
                .Must(p => GeoRequestValidator<CheckInCommand>.AllowedPhotoContentTypes.Contains(p.ContentType))
                .WithMessage("Faqat rasm (JPEG, PNG, WebP, HEIC) qabul qilinadi.")
                .Must(p => !string.IsNullOrWhiteSpace(p.FileName) && p.FileName.Trim().Length <= StoredFile.FileNameMaxLength)
                .WithMessage("Rasm nomi bo'sh yoki juda uzun.")
                .OverridePropertyName("Photo");
        });
    }
}

internal sealed class SubmitStudentFaceCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileStorage storage, IFaceEngine faceEngine,
    IAuditWriter audit)
    : IRequestHandler<SubmitStudentFaceCommand, StudentFaceDto>
{
    public const string NoFaceMessage = "Rasmda yuz topilmadi. Yuzingiz aniq ko'rinadigan qilib qayta suratga oling.";
    public const string ManyFacesMessage = "Rasmda faqat bitta yuz bo'lishi kerak.";
    public const string UnreadableMessage = "Rasmni o'qib bo'lmadi. JPEG yoki PNG formatda qayta yuboring.";

    public async Task<StudentFaceDto> Handle(SubmitStudentFaceCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var photo = request.Photo!;

        if (!await db.StudentProfiles.AsNoTracking().AnyAsync(p => p.UserId == userId, cancellationToken))
            throw new NotFoundException("Talaba profili topilmadi.");

        // Tasdiqlangan etalonni talaba o'zi almashtira olmaydi — og'ir tahlildan oldin tekshiriladi.
        var existingStatus = await db.StudentFaceEnrollments.AsNoTracking()
            .Where(f => f.StudentUserId == userId)
            .Select(f => (FaceEnrollmentStatus?)f.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingStatus == FaceEnrollmentStatus.Approved)
            throw new ConflictException(StudentFaceEnrollment.AlreadyApprovedMessage);

        var analysis = await faceEngine.AnalyzeOrThrowAsync(photo, cancellationToken);
        var problem = !analysis.Readable ? UnreadableMessage
            : analysis.FaceCount == 0 || analysis.Embedding is null ? NoFaceMessage
            : analysis.FaceCount > 1 ? ManyFacesMessage
            : null;
        if (problem is not null)
            throw new ValidationException(new Dictionary<string, string[]> { ["Photo"] = [problem] }, problem);

        var settings = await db.LoadStudentSettingsAsync(cancellationToken);
        var now = clock.UtcNow;
        string? savedKey = null;
        try
        {
            await using (var content = photo.OpenRead())
                savedKey = await storage.SaveAsync(content, photo.FileName, photo.ContentType, cancellationToken);

            var stored = StoredFile.Create(
                StoredFileKind.FacePhoto, photo.FileName, photo.ContentType, photo.Length, savedKey, now, userId);
            db.StoredFiles.Add(stored);

            var enrollment = await db.StudentFaceEnrollments.FirstOrDefaultAsync(f => f.StudentUserId == userId, cancellationToken);
            var replaced = enrollment is not null;
            if (enrollment is null)
            {
                enrollment = StudentFaceEnrollment.Submit(userId, stored.Id, analysis.Embedding!, now);
                db.StudentFaceEnrollments.Add(enrollment);
            }
            else
            {
                // Oraliqda tasdiqlangan bo'lsa domain 409 beradi; parallel birinchi yuborish — unique indeks → 409.
                enrollment.Resubmit(stored.Id, analysis.Embedding!, now);
            }

            await audit.WriteAsync(
                AuditAction.FaceEnrollmentSubmitted, nameof(StudentFaceEnrollment), enrollment.Id.ToString(),
                changes: JsonSerializer.Serialize(new { photoFileId = stored.Id, replaced }),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            return StudentFaceDto.From(enrollment, settings.FaceVerificationEnabled);
        }
        catch
        {
            if (savedKey is not null)
                await storage.DeleteAsync(savedKey, CancellationToken.None);
            throw;
        }
    }
}

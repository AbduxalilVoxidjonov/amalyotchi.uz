using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Tutor.Applications;

public enum ApplicationDecision
{
    Approve = 1,
    Return = 2,
    Reject = 3
}

/// <summary>Tab hisoblagichlari (kontrakt: <c>counts: Record&lt;ApplicationStatus, number&gt;</c>).</summary>
public sealed record ApplicationCounts(int Submitted, int RevisionNeeded, int Approved, int Rejected);

public sealed record ApplicationSummary(
    Guid Id,
    Guid StudentId,
    string Name,
    string Group,
    int Course,
    string HemisId,
    string Company,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt);

public sealed record ApplicationListResponse(ApplicationCounts Counts, IReadOnlyList<ApplicationSummary> Items);

public sealed record GeoCoords(double Lat, double Lng);

public sealed record ApplicationCompany(
    string Name,
    string Tin,
    string Activity,
    string Address,
    string SupervisorName,
    string SupervisorPhone,
    string? MentorName,
    string? MentorPhone);

public sealed record ApplicationContract(string Name, int? Pages, long SizeBytes, string Url);

/// <summary>Ariza tafsiloti. <c>company</c> — nom (ro'yxat bilan bir xil), <c>companyDetails</c> — to'liq karta;
/// <c>radiusM</c> — joriy taklif (tasdiqlangach tyutor qiymati), <c>checklist</c> — belgilangan punktlar.</summary>
public sealed record ApplicationDetail(
    Guid Id,
    Guid StudentId,
    string Name,
    string Group,
    int Course,
    string HemisId,
    string Company,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    GeoCoords Coords,
    int RadiusM,
    ApplicationCompany CompanyDetails,
    ApplicationContract? Contract,
    string? Comment,
    IReadOnlyList<int> Checklist,
    int RevisionCount);

public sealed record ApplicationDecisionResponse(Guid Id, ApplicationStatus Status);

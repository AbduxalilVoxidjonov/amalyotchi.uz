namespace Amaliyotchi.Domain.Practice;

/// <summary>Amaliyot arizasi (korxona + shartnoma) holati. JSON'da camelCase: submitted, revisionNeeded …</summary>
public enum ApplicationStatus
{
    Draft = 1,
    Submitted = 2,
    RevisionNeeded = 3,
    Approved = 4,
    Rejected = 5,
    Completed = 6
}

namespace Amaliyotchi.Domain.Files;

public enum StoredFileKind
{
    Contract = 1,
    DiaryAttachment = 2,
    LeaveDocument = 3,
    Template = 4,

    /// <summary>Check-in/check-out paytida olingan selfi (rad etilgan urinishniki ham saqlanadi).</summary>
    CheckInPhoto = 5,

    /// <summary>Talabaning etalon yuz rasmi ("Yuzni tasdiqlash") — <c>StudentFaceEnrollment.PhotoFileId</c>.</summary>
    FacePhoto = 6
}

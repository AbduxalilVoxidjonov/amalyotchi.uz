/**
 * Kontrakt v2 `AuditEntry` (backend `AuditEntryDto`). `action` — `AuditAction.cs` enum, camelCase string;
 * matn (label, tafsilot, kim) frontend'da yasaladi — `describe.ts`.
 */
export const AUDIT_ACTIONS = [
  'created',
  'updated',
  'deleted',
  'manualOverride',
  'loggedIn',
  'loginFailed',
  'manualCheckIn',
  'radiusChanged',
  'applicationApproved',
  'applicationReturned',
  'applicationRejected',
  'leaveApproved',
  'leaveRejected',
  'diaryReviewed',
  'gradeChanged',
  'gradeReverted',
  'settingsChanged',
  'attendanceMarkedSuspicious',
  'studentCompanyReassigned',
  'facultyCreated',
  'facultyUpdated',
  'facultyDeleted',
  'facultyActivated',
  'facultyDeactivated',
  'departmentCreated',
  'departmentUpdated',
  'departmentDeleted',
  'departmentActivated',
  'departmentDeactivated',
  'directionCreated',
  'directionUpdated',
  'directionDeleted',
  'directionActivated',
  'directionDeactivated',
  'groupCreated',
  'groupUpdated',
  'groupDeleted',
  'groupActivated',
  'groupDeactivated',
  'tutorCreated',
  'tutorUpdated',
  'tutorActivated',
  'tutorDeactivated',
  'tutorPasswordReset',
  'tutorScopesChanged',
  'studentsImported',
  'studentCreated',
  'companyCreated',
  'companyUpdated',
  'companyActivated',
  'companyDeactivated',
  'companyDeleted',
  'companiesImported',
  'studentsAssignedToCompany',
  'companyQrRotated',
  'practicePeriodCreated',
  'practicePeriodUpdated',
  'practicePeriodGroupsChanged',
  'practicePeriodClosed',
  'practicePeriodDeleted',
  'studentPasswordSet',
  'passwordChanged',
  'telegramLinked',
  'loginChanged',
  'broadcastMessageCreated',
  'broadcastMessageRetried',
  'studentWorkHoursChanged',
  'faceEnrollmentSubmitted',
  'faceEnrollmentApproved',
  'faceEnrollmentRejected',
  'faceEnrollmentReset',
  // v3.28 — bosh admin adminlarni boshqaradi (tyutor amallari tarix uchun qoldi).
  'adminCreated',
  'adminUpdated',
  'adminStatusChanged',
  'adminPasswordReset',
  // v3.34 — admin talabaning HEMIS ID sini (ilova logini) o'zgartirdi.
  'studentHemisIdChanged',
] as const;

export type AuditAction = (typeof AUDIT_ACTIONS)[number];

/** `UserRole.cs` — JSON'da camelCase. `tutor` — v3.28 dan oldingi tarixiy yozuvlar uchun. */
export type AuditUserRole = 'admin' | 'tutor' | 'student';

export interface AuditEntry {
  id: string;
  /** ISO 8601 (UI: "12.10 09:31"). */
  at: string;
  action: AuditAction;
  /** Entity nomi: "User", "PracticeApplication", "AppSetting"… */
  entityName: string;
  entityId: string | null;
  reason: string | null;
  /** JSON matni: `{"Field":{"old":…,"new":…}}` (yoki null). */
  changes: string | null;
  userId: string | null;
  userName: string | null;
  userRole: AuditUserRole | null;
}

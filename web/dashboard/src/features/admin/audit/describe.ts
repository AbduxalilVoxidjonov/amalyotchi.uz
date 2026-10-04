import type { StatusKind } from '@/shared/ui';
import { DASH } from '../shared/format';
import type { AuditAction, AuditEntry, AuditUserRole } from './types';

/** Amal → o'zbekcha label + Badge rangi (`AuditAction.cs` barcha qiymatlari). */
export const AUDIT_ACTION_LABEL: Record<AuditAction, { label: string; kind: StatusKind }> = {
  created: { label: 'Yaratildi', kind: 'neu' },
  updated: { label: 'Yangilandi', kind: 'neu' },
  deleted: { label: "O'chirildi", kind: 'bad' },
  manualOverride: { label: "Qo'lda aralashuv", kind: 'late' },
  loggedIn: { label: 'Tizimga kirdi', kind: 'info' },
  loginFailed: { label: 'Kirish xato', kind: 'bad' },
  manualCheckIn: { label: "Qo'lda check-in", kind: 'late' },
  radiusChanged: { label: "Radius o'zgardi", kind: 'info' },
  applicationApproved: { label: 'Ariza tasdiqlandi', kind: 'ok' },
  applicationReturned: { label: 'Ariza qaytarildi', kind: 'late' },
  applicationRejected: { label: 'Ariza rad etildi', kind: 'bad' },
  leaveApproved: { label: 'Sababli tasdiqlandi', kind: 'ok' },
  leaveRejected: { label: 'Sababli rad etildi', kind: 'bad' },
  diaryReviewed: { label: 'Kundalik tekshirildi', kind: 'ok' },
  gradeChanged: { label: "Baho o'zgardi", kind: 'info' },
  gradeReverted: { label: 'Baho bekor qilindi', kind: 'bad' },
  settingsChanged: { label: "Sozlama o'zgardi", kind: 'info' },
  attendanceMarkedSuspicious: { label: 'Shubhali davomat', kind: 'bad' },
  studentCompanyReassigned: { label: "Talaba korxonasi o'zgartirildi", kind: 'neu' },
  facultyCreated: { label: 'Fakultet yaratildi', kind: 'ok' },
  facultyUpdated: { label: 'Fakultet tahrirlandi', kind: 'neu' },
  facultyDeleted: { label: "Fakultet o'chirildi", kind: 'bad' },
  facultyActivated: { label: 'Fakultet faollashtirildi', kind: 'ok' },
  facultyDeactivated: { label: 'Fakultet nofaol qilindi', kind: 'late' },
  departmentCreated: { label: 'Kafedra yaratildi', kind: 'ok' },
  departmentUpdated: { label: 'Kafedra tahrirlandi', kind: 'neu' },
  departmentDeleted: { label: "Kafedra o'chirildi", kind: 'bad' },
  departmentActivated: { label: 'Kafedra faollashtirildi', kind: 'ok' },
  departmentDeactivated: { label: 'Kafedra nofaol qilindi', kind: 'late' },
  directionCreated: { label: "Yo'nalish yaratildi", kind: 'ok' },
  directionUpdated: { label: "Yo'nalish tahrirlandi", kind: 'neu' },
  directionDeleted: { label: "Yo'nalish o'chirildi", kind: 'bad' },
  directionActivated: { label: "Yo'nalish faollashtirildi", kind: 'ok' },
  directionDeactivated: { label: "Yo'nalish nofaol qilindi", kind: 'late' },
  groupCreated: { label: 'Guruh yaratildi', kind: 'ok' },
  groupUpdated: { label: 'Guruh tahrirlandi', kind: 'neu' },
  groupDeleted: { label: "Guruh o'chirildi", kind: 'bad' },
  groupActivated: { label: 'Guruh faollashtirildi', kind: 'ok' },
  groupDeactivated: { label: 'Guruh nofaol qilindi', kind: 'late' },
  tutorCreated: { label: 'Tyutor yaratildi', kind: 'ok' },
  tutorUpdated: { label: 'Tyutor tahrirlandi', kind: 'neu' },
  tutorActivated: { label: 'Tyutor faollashtirildi', kind: 'ok' },
  tutorDeactivated: { label: 'Tyutor nofaol qilindi', kind: 'late' },
  tutorPasswordReset: { label: 'Tyutor paroli tiklandi', kind: 'late' },
  tutorScopesChanged: { label: "Tyutor biriktiruvi o'zgardi", kind: 'info' },
  studentsImported: { label: 'Talabalar import qilindi', kind: 'ok' },
  studentCreated: { label: "Talaba qo'shildi", kind: 'ok' },
  companyCreated: { label: 'Korxona yaratildi', kind: 'ok' },
  companyUpdated: { label: 'Korxona tahrirlandi', kind: 'neu' },
  companyActivated: { label: 'Korxona faollashtirildi', kind: 'ok' },
  companyDeactivated: { label: 'Korxona nofaol qilindi', kind: 'late' },
  companyDeleted: { label: "Korxona o'chirildi", kind: 'bad' },
  companiesImported: { label: 'Korxonalar import qilindi', kind: 'ok' },
  studentsAssignedToCompany: { label: 'Talabalar korxonaga biriktirildi', kind: 'info' },
  companyQrRotated: { label: 'Korxona QR kodi yangilandi', kind: 'info' },
  practicePeriodCreated: { label: 'Amaliyot davri yaratildi', kind: 'ok' },
  practicePeriodUpdated: { label: 'Amaliyot davri tahrirlandi', kind: 'neu' },
  practicePeriodGroupsChanged: { label: "Davr guruhlari o'zgardi", kind: 'info' },
  practicePeriodClosed: { label: 'Amaliyot davri yopildi', kind: 'late' },
  practicePeriodDeleted: { label: "Amaliyot davri o'chirildi", kind: 'bad' },
  studentPasswordSet: { label: "Talabaga parol o'rnatildi", kind: 'info' },
  passwordChanged: { label: 'Parol almashtirildi', kind: 'info' },
  telegramLinked: { label: "Telegram bog'landi", kind: 'ok' },
  loginChanged: { label: 'Login almashtirildi', kind: 'info' },
  broadcastMessageCreated: { label: 'Xabar yuborildi', kind: 'info' },
  broadcastMessageRetried: { label: 'Xabar qayta yuborildi', kind: 'info' },
  studentWorkHoursChanged: { label: "Talaba ish vaqtini o'zgartirdi", kind: 'info' },
  faceEnrollmentSubmitted: { label: 'Talaba yuz rasmini yubordi', kind: 'info' },
  faceEnrollmentApproved: { label: 'Yuz rasmi tasdiqlandi', kind: 'ok' },
  faceEnrollmentRejected: { label: 'Yuz rasmi rad etildi', kind: 'bad' },
  faceEnrollmentReset: { label: 'Yuz rasmi bekor qilindi', kind: 'late' },
};

export const USER_ROLE_LABEL: Record<AuditUserRole, string> = {
  admin: 'admin',
  tutor: 'tyutor',
  student: 'talaba',
};

/** Backend entity nomi → o'zbekcha; ro'yxatda yo'qi — xom nom. */
const ENTITY_LABEL: Record<string, string> = {
  User: 'Foydalanuvchi',
  StudentProfile: 'Talaba',
  StudentGroup: 'Guruh',
  Faculty: 'Fakultet',
  Company: 'Korxona',
  PracticeApplication: 'Ariza',
  PracticePeriod: 'Amaliyot davri',
  PracticeGrade: 'Baho',
  DailyAttendance: 'Davomat',
  DiaryEntry: 'Kundalik',
  LeaveRequest: "Sababli so'rov",
  AppSetting: 'Sozlama',
  Holiday: 'Bayram',
  DocumentTemplate: 'Hujjat shabloni',
};

export function entityLabel(entityName: string): string {
  return ENTITY_LABEL[entityName] ?? entityName;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Guid → qisqa "01a0a0e0" (matnda o'qilishi uchun); boshqa id (masalan sozlama kaliti) — o'zgarishsiz. */
function shortId(id: string): string {
  return GUID.test(id) ? id.slice(0, 8) : id;
}

type ChangeMap = Record<string, { old?: unknown; new?: unknown }>;

function valueText(v: unknown): string {
  if (v == null || v === '') return DASH;
  if (typeof v === 'object') return JSON.stringify(v);
  return String(v);
}

/** `changes` JSON → "geofenceRadius: 200 → 250, workDays: … → …". Noto'g'ri JSON — xom matn. */
export function formatChanges(changes: string | null): string | null {
  if (!changes) return null;
  let parsed: unknown;
  try {
    parsed = JSON.parse(changes);
  } catch {
    return changes;
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return changes;
  const entries = Object.entries(parsed as ChangeMap);
  if (entries.length === 0) return null;
  return entries
    .map(([key, ch]) =>
      ch && typeof ch === 'object' && ('old' in ch || 'new' in ch)
        ? `${key}: ${valueText(ch.old)} → ${valueText(ch.new)}`
        : `${key}: ${valueText(ch)}`,
    )
    .join(', ');
}

/** Tafsilot matni: "Ariza 01a0a0e0 — sabab: … · geofenceRadius: 200 → 250". */
export function auditDetail(entry: AuditEntry): string {
  const subject = entry.entityId
    ? `${entityLabel(entry.entityName)} ${shortId(entry.entityId)}`
    : entityLabel(entry.entityName);
  const parts = [subject];
  if (entry.reason) parts.push(`sabab: ${entry.reason}`);
  const changes = formatChanges(entry.changes);
  if (changes) parts.push(changes);
  return parts.join(' — ');
}

/** "N. Saidova · tyutor"; foydalanuvchisiz (interceptor) yozuv — "Tizim". */
export function auditWho(entry: AuditEntry): string {
  if (!entry.userName) return 'Tizim';
  const role = entry.userRole ? USER_ROLE_LABEL[entry.userRole] : null;
  return role ? `${entry.userName} · ${role}` : entry.userName;
}

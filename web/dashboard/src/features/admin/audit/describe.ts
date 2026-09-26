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

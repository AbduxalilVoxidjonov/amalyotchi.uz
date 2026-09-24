import { parseJwt, toUserRole, type UserSummaryDto } from '@amaliyotchi/shared';
import type { TwaAuthResult } from '@/shared/auth/session';
import { resetDiaryMocks } from '@/features/diary/mocks';
import { resetPeriodDaysMocks } from '@/features/period-days/mocks';
import { resetPlaceMocks } from '@/features/place/mocks';
import { resetTodayMocks } from '@/features/today/mocks';

export const mockStudent: UserSummaryDto = {
  id: '33333333-3333-4333-8333-333333333333',
  // Familiya avval — dashboard mock'lari (HEMIS tartibi) bilan bir xil; dizayn matni "Akmal Aliyev" ❓.
  fullName: 'Aliyev Akmal',
  // Backend v2: JsonStringEnumConverter(camelCase) → "student" (eski javoblarda 3 raqami).
  role: 'student',
  facultyId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  phoneNumber: '+998901112233',
  groupId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  groupName: '412-22',
  course: 3,
  hemisId: '341030',
};

/** Ikkinchi talaba (web-login): vaqtinchalik parol, amaliyot davri va tyutor biriktirilmagan. */
export const mockStudentNew: UserSummaryDto = {
  id: '44444444-4444-4444-8444-444444444444',
  fullName: 'Karimova Dilnoza',
  role: 'student',
  facultyId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  phoneNumber: null,
  groupId: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbc',
  groupName: '411-22',
  course: 3,
  hemisId: '341031',
};

/** Xodim (tyutor) — TWA web-login'da rad etilishi kerak. */
export const mockTutor: UserSummaryDto = {
  id: '22222222-2222-4222-8222-222222222222',
  fullName: 'Saidova Nodira',
  role: 'tutor',
  facultyId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  phoneNumber: '+998901234567',
  groupId: null,
  groupName: null,
  course: null,
  hemisId: '100000000002',
};

export interface MockAccount {
  user: UserSummaryDto;
  password: string;
  mustChangePassword: boolean;
}

/**
 * Web-login mock hisoblari (HEMIS ID → hisob). Parollar:
 *   341030 / talaba12345  — Aliyev Akmal (davr bor, parol doimiy)
 *   341031 / vaqtincha1   — Karimova Dilnoza (mustChangePassword, davr/tyutor yo'q)
 *   100000000002 / tyutor123 — tyutor (TWA rad etadi)
 */
function seedAccounts(): Map<string, MockAccount> {
  return new Map([
    ['341030', { user: mockStudent, password: 'talaba12345', mustChangePassword: false }],
    ['341031', { user: mockStudentNew, password: 'vaqtincha1', mustChangePassword: true }],
    ['100000000002', { user: mockTutor, password: 'tyutor123', mustChangePassword: false }],
  ]);
}

export let mockAccounts = seedAccounts();

export function accountByUserId(userId: string | null | undefined): MockAccount | undefined {
  if (!userId) return undefined;
  for (const a of mockAccounts.values()) if (a.user.id === userId) return a;
  return undefined;
}

/** Bearer JWT `sub` (mock token imzosiz) → hisob. */
export function accountFromRequest(request: Request): MockAccount | undefined {
  const token = request.headers.get('authorization')?.replace(/^Bearer\s+/i, '');
  return accountByUserId(parseJwt(token)?.sub);
}

export const mockSessions = new Map<string, string>();

/**
 * Telegram bog'lashlar (mock): initData → userId. `POST /api/auth/telegram/link` yozadi;
 * shundan keyin `/api/auth/telegram` shu initData bilan (masalan `unlinked`) avtomatik kiradi.
 */
export const mockTelegramLinks = new Map<string, string>();

function base64Url(input: string): string {
  const bytes = new TextEncoder().encode(input);
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function issueSession(user: UserSummaryDto, ttlSeconds = 1800): TwaAuthResult {
  const now = Math.floor(Date.now() / 1000);
  const payload = {
    sub: user.id,
    name: user.fullName,
    role: toUserRole(user.role) ?? 'Student',
    faculty_id: user.facultyId,
    iat: now,
    exp: now + ttlSeconds,
  };
  const token = `${base64Url('{"alg":"HS256","typ":"JWT"}')}.${base64Url(JSON.stringify(payload))}.mock`;
  const refreshToken = `mock-refresh-${crypto.randomUUID()}`;
  mockSessions.set(refreshToken, user.id);
  return {
    accessToken: token,
    accessTokenExpiresAt: new Date((now + ttlSeconds) * 1000).toISOString(),
    refreshToken,
    user,
    mustChangePassword: accountByUserId(user.id)?.mustChangePassword ?? false,
  };
}

/**
 * Testlarda har testdan keyin: sessiyalar + o'zgaruvchan talaba holati
 * (check-in, kundalik, amaliyot joyi arizasi).
 */
export function resetMockState() {
  mockSessions.clear();
  mockTelegramLinks.clear();
  mockAccounts = seedAccounts();
  resetTodayMocks();
  resetDiaryMocks();
  resetPlaceMocks();
  resetPeriodDaysMocks();
}

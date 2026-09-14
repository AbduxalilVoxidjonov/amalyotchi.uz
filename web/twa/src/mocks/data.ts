import type { AuthResultDto, UserSummaryDto } from '@amaliyotchi/shared';
import { resetDiaryMocks } from '@/features/diary/mocks';
import { resetLeaveMocks } from '@/features/leave/mocks';
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

export const mockSessions = new Map<string, string>();

function base64Url(input: string): string {
  const bytes = new TextEncoder().encode(input);
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function issueSession(user: UserSummaryDto, ttlSeconds = 1800): AuthResultDto {
  const now = Math.floor(Date.now() / 1000);
  const payload = {
    sub: user.id,
    name: user.fullName,
    role: 'Student',
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
  };
}

/** Testlarda har testdan keyin: sessiyalar + o'zgaruvchan talaba holati (check-in, kundalik, ruxsat). */
export function resetMockState() {
  mockSessions.clear();
  resetTodayMocks();
  resetDiaryMocks();
  resetLeaveMocks();
}

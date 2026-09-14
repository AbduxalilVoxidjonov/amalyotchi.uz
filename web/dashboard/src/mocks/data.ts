import type { AuthResultDto, ProblemDetails, UserSummaryDto } from '@amaliyotchi/shared';

/**
 * Mock foydalanuvchilar. Rol raqamlari UserRole.cs bilan mos (Admin=1, Tutor=2, Student=3).
 * Login: telefon + parol (pastda).
 */
export interface MockUser extends UserSummaryDto {
  password: string;
}

export const mockUsers: MockUser[] = [
  {
    id: '11111111-1111-4111-8111-111111111111',
    fullName: 'Admin Adminov',
    role: 1,
    facultyId: null,
    phoneNumber: '+998901234567',
    password: 'admin12345',
  },
  {
    id: '22222222-2222-4222-8222-222222222222',
    fullName: 'Tyutor Tyutorova',
    role: 2,
    facultyId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    phoneNumber: '+998907654321',
    password: 'tutor12345',
  },
];

const roleName: Record<number, string> = { 1: 'Admin', 2: 'Tutor', 3: 'Student' };

function base64Url(input: string): string {
  const bytes = new TextEncoder().encode(input);
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Imzosiz "JWT" — faqat parseJwt ishlashi uchun (server tekshirmaydi, chunki mock). */
export function makeFakeJwt(
  user: UserSummaryDto,
  ttlSeconds = 30 * 60,
): {
  token: string;
  expiresAt: string;
} {
  const now = Math.floor(Date.now() / 1000);
  const payload: Record<string, unknown> = {
    sub: user.id,
    jti: crypto.randomUUID(),
    name: user.fullName,
    role: typeof user.role === 'number' ? roleName[user.role] : user.role,
    iat: now,
    nbf: now,
    exp: now + ttlSeconds,
    iss: 'amaliyotchi-mock',
    aud: 'amaliyotchi-web',
  };
  if (user.facultyId) payload['faculty_id'] = user.facultyId;
  const header = base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const token = `${header}.${base64Url(JSON.stringify(payload))}.mock-signature`;
  return { token, expiresAt: new Date((now + ttlSeconds) * 1000).toISOString() };
}

/** Sessiyalar: refreshToken → userId (rotatsiya uchun). */
export const mockSessions = new Map<string, string>();

export function issueSession(user: MockUser, ttlSeconds?: number): AuthResultDto {
  const { token, expiresAt } = makeFakeJwt(user, ttlSeconds);
  const refreshToken = `mock-refresh-${crypto.randomUUID()}`;
  mockSessions.set(refreshToken, user.id);
  const { password: _password, ...summary } = user;
  return { accessToken: token, accessTokenExpiresAt: expiresAt, refreshToken, user: summary };
}

export function problem(
  status: number,
  title: string,
  detail: string,
  extra?: Partial<ProblemDetails>,
) {
  return {
    status,
    title,
    detail,
    traceId: `mock-${Math.random().toString(36).slice(2, 10)}`,
    ...extra,
  } satisfies ProblemDetails;
}

/** Testlar orasida holatni tozalash. */
export function resetMockState() {
  mockSessions.clear();
}

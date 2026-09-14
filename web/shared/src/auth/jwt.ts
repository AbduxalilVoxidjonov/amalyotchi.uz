import { toUserRole, type UserRole } from './roles';

/**
 * Access token claim'lari — src/Amaliyotchi.Infrastructure/Identity/JwtTokenService.cs
 *   sub        — user Id (GUID)
 *   name       — FullName
 *   role       — "Admin" | "Tutor" | "Student"
 *   faculty_id — GUID (faqat bo'lsa)
 *   jti, iat, nbf, exp, iss, aud — standart
 */
export interface JwtPayload {
  sub: string;
  name: string;
  role: UserRole;
  facultyId: string | null;
  /** Unix soniya. */
  exp: number | null;
  iat: number | null;
  raw: Record<string, unknown>;
}

function base64UrlDecode(input: string): string {
  const normalized = input.replace(/-/g, '+').replace(/_/g, '/');
  const padded = normalized + '='.repeat((4 - (normalized.length % 4)) % 4);
  const binary = atob(padded);
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

/** Imzoni TEKSHIRMAYDI — faqat UI uchun payload o'qiladi. Buzilgan token → null. */
export function parseJwt(token: string | null | undefined): JwtPayload | null {
  if (!token) return null;
  const parts = token.split('.');
  if (parts.length !== 3 || !parts[1]) return null;

  let raw: Record<string, unknown>;
  try {
    const decoded = JSON.parse(base64UrlDecode(parts[1])) as unknown;
    if (typeof decoded !== 'object' || decoded === null) return null;
    raw = decoded as Record<string, unknown>;
  } catch {
    return null;
  }

  const sub = typeof raw['sub'] === 'string' ? raw['sub'] : null;
  const role = toUserRole(raw['role']);
  if (!sub || !role) return null;

  return {
    sub,
    name: typeof raw['name'] === 'string' ? raw['name'] : '',
    role,
    facultyId: typeof raw['faculty_id'] === 'string' ? raw['faculty_id'] : null,
    exp: typeof raw['exp'] === 'number' ? raw['exp'] : null,
    iat: typeof raw['iat'] === 'number' ? raw['iat'] : null,
    raw,
  };
}

/** `skewSeconds` — muddat tugashidan shuncha oldin ham "tugagan" deb hisoblanadi. */
export function isJwtExpired(
  payload: JwtPayload | null,
  now: number = Date.now(),
  skewSeconds = 30,
): boolean {
  if (!payload || payload.exp === null) return true;
  return payload.exp * 1000 <= now + skewSeconds * 1000;
}

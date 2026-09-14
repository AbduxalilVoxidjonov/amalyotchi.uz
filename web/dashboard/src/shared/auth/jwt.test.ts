import { makeFakeJwt } from '@/mocks/data';
import { isJwtExpired, parseJwt } from './jwt';

describe('parseJwt', () => {
  it("sub, name, role, faculty_id, exp ni o'qiydi", () => {
    const { token } = makeFakeJwt({
      id: 'u-1',
      fullName: 'Oʻktam Ergashev',
      role: 2,
      facultyId: 'f-1',
      phoneNumber: null,
    });
    const payload = parseJwt(token);
    expect(payload).not.toBeNull();
    expect(payload!.sub).toBe('u-1');
    expect(payload!.name).toBe('Oʻktam Ergashev');
    expect(payload!.role).toBe('Tutor');
    expect(payload!.facultyId).toBe('f-1');
    expect(payload!.exp).toBeGreaterThan(Date.now() / 1000);
    expect(isJwtExpired(payload)).toBe(false);
  });

  it('buzilgan token → null', () => {
    expect(parseJwt('')).toBeNull();
    expect(parseJwt('abc')).toBeNull();
    expect(parseJwt('a.b.c')).toBeNull();
    expect(parseJwt(null)).toBeNull();
  });

  it("role noma'lum bo'lsa null", () => {
    const b64 = (o: unknown) => btoa(JSON.stringify(o)).replace(/=+$/, '');
    const token = `${b64({ alg: 'none' })}.${b64({ sub: 'x', role: 'Boss' })}.sig`;
    expect(parseJwt(token)).toBeNull();
  });

  it('muddati tugagan token', () => {
    const { token } = makeFakeJwt(
      { id: 'u', fullName: 'A', role: 1, facultyId: null, phoneNumber: null },
      -60,
    );
    expect(isJwtExpired(parseJwt(token))).toBe(true);
  });
});

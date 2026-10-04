import { adminHref } from './present';

describe('adminHref (v3.28 — eski tyutor havolalari)', () => {
  it('/tutor yo‘llari /admin ga moslanadi', () => {
    expect(adminHref('/tutor')).toBe('/admin/today');
    expect(adminHref('/tutor?status=absent')).toBe('/admin/today?status=absent');
    expect(adminHref('/tutor/map')).toBe('/admin/map');
    expect(adminHref('/tutor/applications')).toBe('/admin/applications');
  });

  it("admin havolasi va o'xshash nomlar o'zgarmaydi", () => {
    expect(adminHref('/admin/map')).toBe('/admin/map');
    expect(adminHref('/tutorial')).toBe('/tutorial');
  });
});

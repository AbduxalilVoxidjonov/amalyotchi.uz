import { adminNav, ADMINS_NAV_PATH, roleLabel } from './nav';
import { navBadges, navCrumb, type NavData } from './nav-data';

const admin: NavData = {
  counts: {
    faculties: 11,
    companies: 412,
    students: 1284,
    today: 38,
    applications: 7,
    diaries: 0,
    pendingFaceEnrollments: 3,
  },
  context: { academicYear: '2026-2027' },
};

describe('adminNav', () => {
  it("badges berilmasa — hech bir elementda badge yo'q", () => {
    expect(adminNav().every((it) => it.badge === undefined)).toBe(true);
  });

  it('API sonlari `to` bo‘yicha; 0 — badge yo‘q; katta son — thin space', () => {
    const items = adminNav(navBadges(admin));
    const byTo = Object.fromEntries(items.map((it) => [it.to, it.badge]));
    expect(byTo).toMatchObject({
      '/admin': undefined,
      '/admin/today': '38',
      '/admin/applications': '7',
      '/admin/face': '3',
      '/admin/diaries': undefined,
      '/admin/calendar': undefined,
      '/admin/faculties': '11',
      '/admin/companies': '412',
      '/admin/students': '1\u2009284',
      '/admin/reports': undefined,
    });
  });

  it("eski server (sobiq tyutor sonlari yo'q) — o'sha bandlarda badge yo'q", () => {
    const old: NavData = {
      counts: { faculties: 1, companies: 2, students: 3 },
      context: { academicYear: null },
    };
    const b = navBadges(old);
    expect(b['/admin/today']).toBeUndefined();
    expect(b['/admin/face']).toBeUndefined();
    expect(b['/admin/students']).toBe('3');
  });

  it("'Bugun' yuqorida (Dashboard'dan keyin); 'Tyutorlar' bandi yo'q", () => {
    const items = adminNav();
    expect(items[0]!.to).toBe('/admin');
    expect(items[1]).toMatchObject({ label: 'Bugun', to: '/admin/today' });
    expect(items.some((it) => /tyutor/i.test(it.label) || it.to.includes('tutor'))).toBe(false);
  });

  it("'Adminlar' — faqat bosh admin uchun", () => {
    expect(adminNav().some((it) => it.to === ADMINS_NAV_PATH)).toBe(false);
    expect(
      adminNav(undefined, { isSuperAdmin: false }).some((it) => it.to === ADMINS_NAV_PATH),
    ).toBe(false);
    expect(
      adminNav(undefined, { isSuperAdmin: true }).some((it) => it.to === ADMINS_NAV_PATH),
    ).toBe(true);
  });

  it('rol yozuvi', () => {
    expect(roleLabel(false)).toBe("Admin · o'quv bo'limi");
    expect(roleLabel(true)).toBe("Bosh admin · o'quv bo'limi");
  });
});

describe('navCrumb', () => {
  it('o‘quv yili bilan / null → faqat "Admin"', () => {
    expect(navCrumb(admin)).toBe('Admin · 2026-2027');
    expect(navCrumb({ ...admin, context: { academicYear: null } })).toBe('Admin');
  });

  it('yuklanish/xato (data yo\'q) — faqat "Admin"', () => {
    expect(navCrumb(undefined)).toBe('Admin');
  });
});

import {
  activeGroupId,
  adminNav,
  adminNavGroups,
  ADMINS_NAV_PATH,
  groupBadge,
  roleLabel,
} from './nav';
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

describe('adminNavGroups', () => {
  const shape = (super_: boolean) =>
    adminNavGroups(undefined, { isSuperAdmin: super_ }).map((g) => [
      g.label,
      g.items.map((it) => it.label),
    ]);

  it("bo'limlar va ularning sahifalari (oddiy admin — 'Adminlar' yo'q)", () => {
    expect(shape(false)).toEqual([
      ['Asosiy', ['Dashboard', 'Bugun', 'Xarita']],
      ['Talabalar', ['Talabalar', 'Arizalar', 'Yuz tasdiqlash', 'Kundaliklar', 'Baholash']],
      ['Tuzilma', ['Fakultetlar']],
      ['Amaliyot', ['Amaliyot davrlari', 'Korxonalar', 'Kalendar']],
      ['Aloqa va hisobot', ['Xabarlar', 'Hisobotlar']],
      ['Tizim', ['Audit jurnali', 'Sozlamalar']],
    ]);
  });

  it("bosh admin — 'Tizim' bo'limida 'Adminlar' birinchi", () => {
    expect(shape(true).at(-1)).toEqual(['Tizim', ['Adminlar', 'Audit jurnali', 'Sozlamalar']]);
  });

  it("har sahifa faqat bitta bo'limda; tekis ro'yxat bo'limlar tartibida", () => {
    const groups = adminNavGroups(undefined, { isSuperAdmin: true });
    const flat = groups.flatMap((g) => g.items.map((it) => it.to));
    expect(new Set(flat).size).toBe(flat.length);
    expect(adminNav(undefined, { isSuperAdmin: true }).map((it) => it.to)).toEqual(flat);
  });

  it("aktiv bo'lim — eng uzun mos prefiks bo'yicha (ichki sahifalar ham)", () => {
    const groups = adminNavGroups();
    expect(activeGroupId(groups, '/admin')).toBe('main');
    expect(activeGroupId(groups, '/admin/students/42')).toBe('students');
    expect(activeGroupId(groups, '/admin/faculties/1/departments/2')).toBe('structure');
    expect(activeGroupId(groups, '/admin/practice-periods/new')).toBe('practice');
    expect(activeGroupId(groups, '/admin/settings')).toBe('system');
    expect(activeGroupId(groups, '/boshqa')).toBeUndefined();
  });

  it("bo'lim badge'i — faqat kutilayotgan ish sonlari yig'indisi; ma'lumot sonlari kirmaydi", () => {
    const groups = adminNavGroups(navBadges(admin));
    const byId = Object.fromEntries(groups.map((g) => [g.id, groupBadge(g)]));
    // Talabalar: arizalar 7 + yuz 3 + kundaliklar 0 = 10 (talabalar jami 1284 — kirmaydi).
    expect(byId).toEqual({
      main: undefined, // Bugun (38) — bugungi jadval qatorlari, ish emas
      students: '10',
      structure: undefined, // fakultetlar jami
      practice: undefined, // korxonalar jami
      comms: undefined,
      system: undefined,
    });
    expect(adminNavGroups().every((g) => groupBadge(g) === undefined)).toBe(true);
  });

  it('kutilayotgan ish bandlari: Arizalar, Yuz tasdiqlash, Kundaliklar; katta son formatlanadi', () => {
    const items = adminNav(undefined, { isSuperAdmin: true });
    expect(items.filter((it) => it.pending).map((it) => it.to)).toEqual([
      '/admin/applications',
      '/admin/face',
      '/admin/diaries',
    ]);
    const big = adminNavGroups(
      navBadges({ ...admin, counts: { ...admin.counts, applications: 1200, diaries: 34 } }),
    );
    expect(groupBadge(big.find((g) => g.id === 'students')!)).toBe('1\u2009237');
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

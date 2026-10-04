import { UserRole } from '@amaliyotchi/shared/auth';
import { navForRole } from './nav';
import { formatCrumbGroups, navBadges, navCrumb, type NavData } from './nav-data';

const admin: NavData = {
  role: UserRole.Admin,
  counts: { faculties: 11, tutors: 0, companies: 412, students: 1284 },
  context: { academicYear: '2026-2027' },
};

const tutor: NavData = {
  role: UserRole.Tutor,
  counts: { today: 38, applications: 7, students: 38, diaries: 0, pendingFaceEnrollments: 3 },
  context: { groups: ['412-22', '413-22'], periodName: '3-kurs amaliyoti' },
};

describe('navForRole', () => {
  it("badges berilmasa — hech bir elementda badge yo'q", () => {
    expect(navForRole(UserRole.Admin).every((it) => it.badge === undefined)).toBe(true);
    expect(navForRole(UserRole.Tutor).every((it) => it.badge === undefined)).toBe(true);
  });

  it('admin: API sonlari `to` bo‘yicha; 0 — badge yo‘q; katta son — thin space', () => {
    const items = navForRole(UserRole.Admin, navBadges(admin));
    const byTo = Object.fromEntries(items.map((it) => [it.to, it.badge]));
    expect(byTo).toMatchObject({
      '/admin': undefined,
      '/admin/faculties': '11',
      '/admin/tutors': undefined,
      '/admin/companies': '412',
      '/admin/students': '1 284',
      '/admin/reports': undefined,
    });
  });

  it('tyutor: today/applications/students/diaries xaritasi', () => {
    const items = navForRole(UserRole.Tutor, navBadges(tutor));
    const byTo = Object.fromEntries(items.map((it) => [it.to, it.badge]));
    expect(byTo).toMatchObject({
      '/tutor': '38',
      '/tutor/applications': '7',
      '/tutor/students': '38',
      '/tutor/diaries': undefined,
      '/tutor/face': '3',
      '/tutor/calendar': undefined,
    });
  });

  it("tyutor: `pendingFaceEnrollments` yo'q (eski server) yoki yuqori darajada keladi", () => {
    const { pendingFaceEnrollments: _omit, ...oldCounts } = (
      tutor as Extract<NavData, { context: { groups: string[] } }>
    ).counts;
    const old = { ...tutor, counts: oldCounts } as NavData;
    expect(navBadges(old)['/tutor/face']).toBeUndefined();
    const top = { ...old, pendingFaceEnrollments: 2 } as NavData;
    expect(navBadges(top)['/tutor/face']).toBe('2');
  });
});

describe('navCrumb', () => {
  it('admin: o‘quv yili bilan / null → faqat "Admin"', () => {
    expect(navCrumb(UserRole.Admin, admin)).toBe('Admin · 2026-2027');
    expect(navCrumb(UserRole.Admin, { ...admin, context: { academicYear: null } } as NavData)).toBe(
      'Admin',
    );
  });

  it("yuklanish/xato (data yo'q) — faqat rol nomi", () => {
    expect(navCrumb(UserRole.Admin, undefined)).toBe('Admin');
    expect(navCrumb(UserRole.Tutor, undefined)).toBe('Tyutor');
  });

  it("tyutor: guruhlar va davr; bo'sh qismlar tashlanadi", () => {
    expect(navCrumb(UserRole.Tutor, tutor)).toBe('Tyutor · 412-22, 413-22 · 3-kurs amaliyoti');
    expect(
      navCrumb(UserRole.Tutor, {
        ...tutor,
        context: { groups: [], periodName: '3-kurs amaliyoti' },
      } as NavData),
    ).toBe('Tyutor · 3-kurs amaliyoti');
    expect(
      navCrumb(UserRole.Tutor, {
        ...tutor,
        context: { groups: ['412-22'], periodName: null },
      } as NavData),
    ).toBe('Tyutor · 412-22');
    expect(
      navCrumb(UserRole.Tutor, { ...tutor, context: { groups: [], periodName: null } } as NavData),
    ).toBe('Tyutor');
  });
});

describe('formatCrumbGroups', () => {
  it('3 tagacha — hammasi; ko‘p bo‘lsa — 2 ta + qolgani soni', () => {
    expect(formatCrumbGroups([])).toBe('');
    expect(formatCrumbGroups(['412-22', '413-22', '414-22'])).toBe('412-22, 413-22, 414-22');
    expect(formatCrumbGroups(['412-22', '413-22', '414-22', '415-22'])).toBe('412-22, 413-22 +2');
    expect(formatCrumbGroups(['a', 'b', 'c', 'd', 'e'])).toBe('a, b +3');
  });
});

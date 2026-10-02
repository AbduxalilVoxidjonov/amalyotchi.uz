import type { StudentFilters } from '../students/types';
import { describeFilter, hasAnyFilter, needsConfirm, targetCount, toAudience } from './audience';
import type { MessageRecipientRow } from './types';

const row: MessageRecipientRow = {
  userId: 'u1',
  fullName: 'Aliyev Akmal',
  hemisId: '341030',
  telegramUserId: 5100000000,
  telegramLinkedAt: null,
  botBlocked: false,
  facultyName: null,
  directionName: null,
  groupName: null,
  course: null,
};

const options: StudentFilters = {
  faculties: [{ id: 'f1', name: 'Axborot texnologiyalari' }],
  directions: [{ id: 'dir1', name: 'Kompyuter injiniringi', facultyId: 'f1' }],
  courses: [2, 3],
};

describe('audience', () => {
  it('bitta talaba — bitta id li `selected` auditoriya, tasdiqsiz', () => {
    const target = { kind: 'single', recipient: row } as const;
    expect(targetCount(target)).toBe(1);
    expect(needsConfirm(target)).toBe(false);
    expect(toAudience(target)).toEqual({ kind: 'selected', userIds: ['u1'] });
  });

  it("tanlanganlar: 20 tagacha tasdiqsiz, 20 dan ko'p — tasdiq bilan", () => {
    const ids = (n: number) => Array.from({ length: n }, (_, i) => `u${i}`);
    expect(needsConfirm({ kind: 'selected', userIds: ids(20) })).toBe(false);
    expect(needsConfirm({ kind: 'selected', userIds: ids(21) })).toBe(true);
    expect(toAudience({ kind: 'selected', userIds: ['a', 'b'] })).toEqual({
      kind: 'selected',
      userIds: ['a', 'b'],
    });
  });

  it('filtr va hammaga — doim tasdiq; auditoriya shakli kontrakt bo‘yicha', () => {
    const filter = {
      kind: 'filter',
      filter: { facultyId: 'f1', course: 3 },
      description: '',
      estimate: 5,
    } as const;
    expect(needsConfirm(filter)).toBe(true);
    expect(targetCount(filter)).toBe(5);
    expect(toAudience(filter)).toEqual({ kind: 'filter', facultyId: 'f1', course: 3 });
    expect(needsConfirm({ kind: 'all', estimate: 3 })).toBe(true);
    expect(toAudience({ kind: 'all', estimate: 3 })).toEqual({ kind: 'all' });
  });

  it('filtr tavsifi variant nomlaridan yasaladi', () => {
    expect(
      describeFilter(
        { facultyId: 'f1', directionId: 'dir1', course: 3, groupId: 'g1', q: 'ali' },
        options,
        [{ id: 'g1', name: '412-22' }],
      ),
    ).toBe(
      "Fakultet: Axborot texnologiyalari · Yo'nalish: Kompyuter injiniringi · 3-kurs · Guruh: 412-22 · Qidiruv: «ali»",
    );
    // Variantlar hali yuklanmagan — faqat ma'lum qismlar.
    expect(describeFilter({ facultyId: 'f1', course: 2 }, undefined, undefined)).toBe('2-kurs');
  });

  it('hasAnyFilter', () => {
    expect(hasAnyFilter({})).toBe(false);
    expect(hasAnyFilter({ q: 'x' })).toBe(true);
    expect(hasAnyFilter({ groupId: 'g1' })).toBe(true);
  });
});

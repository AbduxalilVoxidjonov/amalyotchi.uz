import { adminNavGroups } from './nav';
import { filterNavGroups, normalizeNavText } from './nav-search';

const groups = adminNavGroups(undefined, { isSuperAdmin: true });
const labels = (q: string) =>
  filterNavGroups(groups, q).flatMap((g) => g.items.map((i) => i.label));

describe('normalizeNavText', () => {
  it("apostrof variantlari (' ` ‘ ’ ʻ ʼ) olib tashlanadi; katta-kichik harfga befarq", () => {
    const variants = ["Yo'nalish", 'Yoʻnalish', 'Yo’nalish', 'Yo‘nalish', 'Yoʼnalish', 'Yo`nalish'];
    for (const v of variants) expect(normalizeNavText(v)).toBe('yonalish');
    expect(normalizeNavText("  G'ALABA  ")).toBe('galaba');
    expect(normalizeNavText('Oʻquv   bo‘limi')).toBe('oquv bolimi');
  });
});

describe('filterNavGroups', () => {
  it("bo'sh so'rov — barcha bo'limlar o'zgarishsiz", () => {
    expect(filterNavGroups(groups, '   ')).toEqual(groups);
  });

  it("nom bo'yicha, katta-kichik harfga befarq; mos bandsiz bo'limlar tashlanadi", () => {
    const res = filterNavGroups(groups, 'KUNDA');
    expect(res.map((g) => g.id)).toEqual(['students']);
    expect(res[0]!.items.map((i) => i.label)).toEqual(['Kundaliklar']);
  });

  it("kalit so'zlar bo'yicha (apostrof variantlari bilan)", () => {
    expect(labels('kafedra')).toEqual(['Fakultetlar']);
    expect(labels('yoʻnalish')).toEqual(['Fakultetlar']);
    expect(labels('yonalish')).toEqual(['Fakultetlar']);
    expect(labels('etalon')).toEqual(['Yuz tasdiqlash']);
    expect(labels('guruh')).toEqual(['Fakultetlar', 'Amaliyot davrlari']);
    expect(labels('e’lon')).toEqual(['Xabarlar']);
  });

  it("bo'lim nomi mos kelsa — butun bo'lim", () => {
    expect(labels('tizim')).toEqual(['Adminlar', 'Audit jurnali', 'Sozlamalar']);
  });

  it("natija yo'q — bo'sh ro'yxat", () => {
    expect(filterNavGroups(groups, 'zzzz')).toEqual([]);
  });
});

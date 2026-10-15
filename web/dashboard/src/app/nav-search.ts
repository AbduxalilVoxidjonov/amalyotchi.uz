import type { AdminNavItem, NavGroup } from './nav';

/**
 * O'zbek lotin apostrof variantlari: ' ` ´ ‘ ’ ʻ ʼ ʹ ′ (o', oʻ, g', g‘ ...). Qidiruvda ularning
 * barchasi olib tashlanadi — "yo'nalish", "yoʻnalish", "yonalish" bir xil.
 */
const APOSTROPHES = /['`´‘’ʻʼʹ′]/g;

/** Qidiruv uchun normallashtirish: kichik harf, diakritika va apostroflarsiz, bo'shliqlar siqilgan. */
export function normalizeNavText(text: string): string {
  return text
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLocaleLowerCase('en-US')
    .replace(APOSTROPHES, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function itemMatches(item: AdminNavItem, q: string): boolean {
  return [item.label, ...(item.keywords ?? [])].some((t) => normalizeNavText(t).includes(q));
}

/**
 * Bo'limlarni so'rov bo'yicha filtrlash. Bo'sh so'rov — o'zgarishsiz. Bo'lim nomi mos kelsa — butun
 * bo'lim; aks holda faqat mos bandlar (nom yoki kalit so'z). Mos bandsiz bo'limlar tashlanadi.
 */
export function filterNavGroups(groups: readonly NavGroup[], query: string): NavGroup[] {
  const q = normalizeNavText(query);
  if (!q) return [...groups];
  const out: NavGroup[] = [];
  for (const g of groups) {
    const items = normalizeNavText(g.label).includes(q)
      ? g.items
      : g.items.filter((it) => itemMatches(it, q));
    if (items.length > 0) out.push({ ...g, items });
  }
  return out;
}

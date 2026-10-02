import type { StudentFilters } from '../students/types';
import type {
  MessageAudience,
  MessageRecipientRow,
  RecipientFilter,
  RecipientGroupOption,
} from './types';

/**
 * Xabar yozish oynasining "kimga" rejimi:
 *  - `single` — jadval qatoridagi "Xabar yozish" (bitta talaba);
 *  - `selected` — belgilangan talabalar (sahifalar bo'ylab to'plangan id'lar);
 *  - `filter` — joriy filtr/qidiruv bo'yicha (taxminiy son — ro'yxat `total`i);
 *  - `all` — barcha ulanganlar.
 */
export type ComposeTarget =
  | { kind: 'single'; recipient: MessageRecipientRow }
  | { kind: 'selected'; userIds: string[] }
  | { kind: 'filter'; filter: RecipientFilter; description: string; estimate: number }
  | { kind: 'all'; estimate: number };

/** Shundan ko'p kishiga (tanlanganlar) yuborishda ham tasdiq so'raladi. */
export const CONFIRM_THRESHOLD = 20;

export function targetCount(target: ComposeTarget): number {
  switch (target.kind) {
    case 'single':
      return 1;
    case 'selected':
      return target.userIds.length;
    default:
      return target.estimate;
  }
}

/** Ommaviy (filtr/hammaga) yoki 20 dan ko'p kishiga yuborish — tasdiq bosqichi bilan. */
export function needsConfirm(target: ComposeTarget): boolean {
  return (
    target.kind === 'filter' || target.kind === 'all' || targetCount(target) > CONFIRM_THRESHOLD
  );
}

/** UI rejimi → API `audience`. Bitta talaba — bitta id'li `selected`. */
export function toAudience(target: ComposeTarget): MessageAudience {
  switch (target.kind) {
    case 'single':
      return { kind: 'selected', userIds: [target.recipient.userId] };
    case 'selected':
      return { kind: 'selected', userIds: [...target.userIds] };
    case 'filter':
      return { kind: 'filter', ...target.filter };
    case 'all':
      return { kind: 'all' };
  }
}

/**
 * Joriy filtr tavsifi: "Fakultet: Axborot texnologiyalari · 3-kurs · Guruh: 412-22 · Qidiruv: «ali»".
 * Variant nomi topilmasa (hali yuklanmagan) — o'sha qism tashlab yuboriladi.
 */
export function describeFilter(
  filter: RecipientFilter,
  options: StudentFilters | undefined,
  groups: readonly RecipientGroupOption[] | undefined,
): string {
  const faculty = options?.faculties.find((f) => f.id === filter.facultyId)?.name;
  const direction = options?.directions.find((d) => d.id === filter.directionId)?.name;
  const group = groups?.find((g) => g.id === filter.groupId)?.name;
  const parts = [
    faculty && `Fakultet: ${faculty}`,
    direction && `Yo'nalish: ${direction}`,
    filter.course ? `${filter.course}-kurs` : null,
    group && `Guruh: ${group}`,
    filter.q ? `Qidiruv: «${filter.q}»` : null,
  ].filter(Boolean);
  return parts.join(' · ');
}

/** Filtr yoki qidiruv berilganmi (aks holda "Ommaviy xabar" — barcha ulanganlarga). */
export function hasAnyFilter(filter: RecipientFilter): boolean {
  return Boolean(
    filter.q || filter.facultyId || filter.directionId || filter.course || filter.groupId,
  );
}

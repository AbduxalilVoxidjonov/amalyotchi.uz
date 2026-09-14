import { createContext } from 'react';

/** Faqat primitiv qiymatlar — effekt deps'ida barqaror (element bo'lsa re-render sikli bo'lardi). */
export interface PageHeaderOverride {
  title?: string | undefined;
  crumb?: string | undefined;
}

export interface PageHeaderContextValue {
  override: PageHeaderOverride;
  setOverride: (next: PageHeaderOverride) => void;
  /** Topbar'ning o'ng tomonidagi slot — `<TopbarActions>` shu yerga portal qiladi. */
  actionsEl: HTMLElement | null;
}

export const PageHeaderContext = createContext<PageHeaderContextValue | null>(null);

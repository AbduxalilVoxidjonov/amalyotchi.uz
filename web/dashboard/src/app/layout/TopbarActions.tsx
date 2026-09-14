import { useContext, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { PageHeaderContext } from './page-header-context';

/**
 * Sahifa ichidan Topbar'ning o'ng tomoniga tugma qo'yish (portal):
 *   <TopbarActions><Button size="sm">Eksport</Button></TopbarActions>
 * Shell tashqarisida (masalan testda yakka render) — hech narsa chizmaydi.
 */
export function TopbarActions({ children }: { children: ReactNode }) {
  const el = useContext(PageHeaderContext)?.actionsEl;
  if (!el) return null;
  return createPortal(children, el);
}

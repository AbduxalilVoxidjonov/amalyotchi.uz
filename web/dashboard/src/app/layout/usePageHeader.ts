import { useContext, useEffect } from 'react';
import { PageHeaderContext, type PageHeaderOverride } from './page-header-context';

/**
 * Sahifa ichidan Topbar sarlavhasini boshqarish:
 *   usePageHeader({ title: 'Talabalarim' })
 * Berilmagan maydonlar nav konfigidan (title/crumb) keladi. Unmount'da tozalanadi.
 * Tugmalar uchun `<TopbarActions>` komponentidan foydalaning.
 */
export function usePageHeader({ title, crumb }: PageHeaderOverride): void {
  const setOverride = useContext(PageHeaderContext)?.setOverride;
  useEffect(() => {
    if (!setOverride) return;
    setOverride({ title, crumb });
    return () => setOverride({});
  }, [setOverride, title, crumb]);
}

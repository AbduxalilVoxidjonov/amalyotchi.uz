import { useCallback, useEffect, useState, useSyncExternalStore } from 'react';

export const SIDEBAR_OPEN_STORAGE_KEY = 'amaliyotchi.sidebar.open';

/** Shu kenglikdan tor — sidebar kontent ustiga chiqadigan drawer (AppShell/Sidebar CSS bilan bir xil). */
export const DRAWER_MQ = '(max-width: 900px)';

function subscribe(cb: () => void): () => void {
  if (typeof window.matchMedia !== 'function') return () => undefined;
  const mq = window.matchMedia(DRAWER_MQ);
  mq.addEventListener('change', cb);
  return () => mq.removeEventListener('change', cb);
}

function isDrawerMode(): boolean {
  // matchMedia yo'q (eski brauzer/test muhiti) — desktop deb hisoblanadi.
  return typeof window.matchMedia === 'function' && window.matchMedia(DRAWER_MQ).matches;
}

function readDesktopOpen(): boolean {
  try {
    return window.localStorage.getItem(SIDEBAR_OPEN_STORAGE_KEY) !== 'false';
  } catch {
    return true;
  }
}

/**
 * Sidebar holati. Desktop'da ☰ sidebar'ni ochib-yopadi va holat sahifalar orasida hamda
 * localStorage'da saqlanadi (sukut: ochiq). Tor ekranda (≤900px) — drawer: sukut yopiq,
 * sahifaga o'tilganda yopiladi (kontentni to'sib qolmasin).
 */
export function useSidebarOpen(pathname: string) {
  const drawer = useSyncExternalStore(subscribe, isDrawerMode, () => false);
  const [desktopOpen, setDesktopOpen] = useState(readDesktopOpen);
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => {
    try {
      window.localStorage.setItem(SIDEBAR_OPEN_STORAGE_KEY, String(desktopOpen));
    } catch {
      // Private rejim / bloklangan storage — holat faqat sessiya davomida saqlanadi.
    }
  }, [desktopOpen]);

  // Marshrut o'zgarganda faqat drawer yopiladi; desktop holati saqlanib qoladi.
  useEffect(() => {
    setDrawerOpen(false);
  }, [pathname]);

  const set = drawer ? setDrawerOpen : setDesktopOpen;
  const toggle = useCallback(() => set((v) => !v), [set]);
  const open = useCallback(() => set(true), [set]);
  const close = useCallback(() => set(false), [set]);

  return { isOpen: drawer ? drawerOpen : desktopOpen, drawer, toggle, open, close };
}

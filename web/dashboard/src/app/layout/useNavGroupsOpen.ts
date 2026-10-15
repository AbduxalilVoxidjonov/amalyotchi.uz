import { useCallback, useEffect, useState } from 'react';

export const NAV_GROUPS_STORAGE_KEY = 'amaliyotchi.sidebar.groups';

type OpenMap = Record<string, boolean>;

function readStored(): OpenMap {
  try {
    const raw = window.localStorage.getItem(NAV_GROUPS_STORAGE_KEY);
    if (!raw) return {};
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};
    const out: OpenMap = {};
    for (const [k, v] of Object.entries(parsed)) if (typeof v === 'boolean') out[k] = v;
    return out;
  } catch {
    return {};
  }
}

function writeStored(map: OpenMap): void {
  try {
    window.localStorage.setItem(NAV_GROUPS_STORAGE_KEY, JSON.stringify(map));
  } catch {
    // Private rejim / bloklangan storage — holat faqat sessiya davomida saqlanadi.
  }
}

/**
 * Sidebar bo'limlarining ochiq/yopiq holati (localStorage'da). Sukut bo'yicha barcha bo'limlar
 * yopiq — faqat sarlavha bosilganda ochiladi (aktiv sahifa bo'limi ham avtomatik ochilmaydi).
 */
export function useNavGroupsOpen() {
  const [open, setOpen] = useState<OpenMap>(readStored);

  useEffect(() => {
    writeStored(open);
  }, [open]);

  const isOpen = useCallback((id: string) => open[id] === true, [open]);

  const toggle = useCallback(
    (id: string) => setOpen((prev) => ({ ...prev, [id]: prev[id] !== true })),
    [],
  );

  return { isOpen, toggle };
}

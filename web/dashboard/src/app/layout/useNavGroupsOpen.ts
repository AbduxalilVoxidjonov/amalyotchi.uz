import { useCallback, useEffect, useState } from 'react';
import type { NavGroupId } from '../nav';

export const NAV_GROUPS_STORAGE_KEY = 'amaliyotchi.sidebar.groups';

/** Saqlangan holat yo'q bo'limlar uchun sukut: faqat "Asosiy" ochiq. */
const DEFAULT_OPEN: ReadonlySet<string> = new Set<NavGroupId>(['main']);

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
 * Sidebar bo'limlarining ochiq/yopiq holati (localStorage'da). Aktiv sahifa joylashgan bo'lim
 * birinchi render'da va aktiv bo'lim o'zgarganda avtomatik ochiladi.
 */
export function useNavGroupsOpen(activeGroup: NavGroupId | undefined) {
  const [open, setOpen] = useState<OpenMap>(() => {
    const stored = readStored();
    return activeGroup ? { ...stored, [activeGroup]: true } : stored;
  });
  const [prevActive, setPrevActive] = useState(activeGroup);

  // Aktiv bo'lim o'zgarishi — render paytida holatni moslashtirish (effektsiz, ortiqcha render yo'q).
  if (activeGroup !== prevActive) {
    setPrevActive(activeGroup);
    if (activeGroup && open[activeGroup] !== true) setOpen({ ...open, [activeGroup]: true });
  }

  useEffect(() => {
    writeStored(open);
  }, [open]);

  const isOpen = useCallback((id: string) => open[id] ?? DEFAULT_OPEN.has(id), [open]);

  const toggle = useCallback(
    (id: string) => setOpen((prev) => ({ ...prev, [id]: !(prev[id] ?? DEFAULT_OPEN.has(id)) })),
    [],
  );

  return { isOpen, toggle };
}

import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { activeGroupId, groupBadge, type AdminNavItem, type NavGroup } from '../nav';
import { filterNavGroups } from '../nav-search';
import styles from './SidebarNavGroups.module.css';
import { useNavGroupsOpen } from './useNavGroupsOpen';

export interface SidebarNavGroupsProps {
  groups: readonly NavGroup[];
  /** Havola bosilganda / Enter bilan o'tilganda (drawer'ni yopish uchun). */
  onNavigate?: (() => void) | undefined;
  /** Klaviatura yorlig'i (`/`, Ctrl/Cmd+K) bosilganda — mobil drawer'ni ochish uchun. */
  onSearchShortcut?: (() => void) | undefined;
}

/** Fokus matn kiritish maydonida bo'lsa `/` yorlig'i ishlamaydi (oddiy belgi sifatida yoziladi). */
function isEditable(el: EventTarget | null): boolean {
  if (!(el instanceof HTMLElement)) return false;
  if (el.isContentEditable) return true;
  const tag = el.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}

function Badge({ value }: { value: AdminNavItem['badge'] }) {
  if (value === undefined || value === '') return null;
  return <span className={styles.badge}>{String(value)}</span>;
}

/**
 * Bo'limlarga ajratilgan sidebar nav + qidiruv. Bo'lim sarlavhasi — `button[aria-expanded]`;
 * yopiq bo'limda ichki badge'lar yig'indisi ko'rinadi. Qidiruvda mos bo'limlar ochiq ko'rsatiladi.
 */
export function SidebarNavGroups({ groups, onNavigate, onSearchShortcut }: SidebarNavGroupsProps) {
  const baseId = useId();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const navRef = useRef<HTMLElement>(null);
  const [query, setQuery] = useState('');

  const active = activeGroupId(groups, pathname);
  const { isOpen, toggle } = useNavGroupsOpen();

  const searching = query.trim() !== '';
  const visible = useMemo(() => filterNavGroups(groups, query), [groups, query]);
  const first = searching ? visible[0]?.items[0] : undefined;
  const resultCount = visible.reduce((n, g) => n + g.items.length, 0);

  // Global yorliqlar: `/` (matn maydonidan tashqarida) va Ctrl/Cmd+K → qidiruvga fokus.
  useEffect(() => {
    const onKey = (e: globalThis.KeyboardEvent) => {
      const modK = (e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() === 'k';
      const slash = e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey && !isEditable(e.target);
      if (!modK && !slash) return;
      e.preventDefault();
      onSearchShortcut?.();
      inputRef.current?.focus();
      inputRef.current?.select();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onSearchShortcut]);

  const onInputKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') {
      e.preventDefault();
      if (query) setQuery('');
      else inputRef.current?.blur();
    } else if (e.key === 'Enter') {
      if (!first) return;
      e.preventDefault();
      setQuery('');
      navigate(first.to);
      onNavigate?.();
    } else if (e.key === 'ArrowDown') {
      const link = navRef.current?.querySelector<HTMLAnchorElement>('a[href]');
      if (link) {
        e.preventDefault();
        link.focus();
      }
    }
  };

  const onLinkClick = () => {
    if (searching) setQuery('');
    onNavigate?.();
  };

  return (
    <div className={styles.root}>
      <div className={styles.search} role="search">
        <label htmlFor={`${baseId}-q`} className={styles.srOnly}>
          Sahifalarni qidirish
        </label>
        <input
          ref={inputRef}
          id={`${baseId}-q`}
          type="search"
          className={styles.input}
          placeholder="Qidirish…"
          autoComplete="off"
          spellCheck={false}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onInputKey}
          aria-describedby={`${baseId}-hint`}
          aria-keyshortcuts="/ Control+K Meta+K"
        />
        <kbd className={styles.kbd} aria-hidden="true">
          /
        </kbd>
        <span id={`${baseId}-hint`} className={styles.srOnly}>
          Enter — birinchi natijaga o'tish, Esc — tozalash
        </span>
      </div>
      {/* Jonli hudud: role="status" emas — sahifalardagi `getByRole('status')` flash'lari bilan to'qnashmasin. */}
      <div className={styles.srOnly} aria-live="polite" aria-atomic="true">
        {searching ? `${resultCount} ta natija` : ''}
      </div>

      <nav ref={navRef} className={styles.nav} aria-label="Asosiy">
        {searching && visible.length === 0 && <p className={styles.empty}>Hech narsa topilmadi</p>}
        {visible.map((g) => {
          const listId = `${baseId}-${g.id}`;
          const open = searching || isOpen(g.id);
          const sum = open ? undefined : groupBadge(g);
          return (
            <div
              key={g.id}
              role="group"
              className={styles.group}
              data-active={g.id === active || undefined}
              aria-labelledby={`${listId}-h`}
            >
              {searching ? (
                <h2 id={`${listId}-h`} className={styles.groupTitle}>
                  {g.label}
                </h2>
              ) : (
                <h2 className={styles.groupHeading}>
                  <button
                    type="button"
                    id={`${listId}-h`}
                    className={styles.groupBtn}
                    aria-expanded={open}
                    aria-controls={listId}
                    onClick={() => toggle(g.id)}
                  >
                    <span className={styles.chevron} aria-hidden="true" />
                    <span className={styles.groupLabel}>{g.label}</span>
                    <Badge value={sum} />
                  </button>
                </h2>
              )}
              <ul id={listId} className={styles.list} hidden={!open}>
                {g.items.map((it) => (
                  <li key={it.to}>
                    <NavLink
                      to={it.to}
                      end={it.end ?? false}
                      className={styles.item ?? ''}
                      data-first={it === first || undefined}
                      onClick={onLinkClick}
                    >
                      <span className={styles.label}>
                        {it.icon}
                        {it.label}
                      </span>
                      <Badge value={it.badge} />
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
      </nav>
    </div>
  );
}

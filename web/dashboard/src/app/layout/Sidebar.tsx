import { Link } from 'react-router-dom';
import { Avatar, Button } from '@/shared/ui';
import type { NavGroup } from '../nav';
import styles from './Sidebar.module.css';
import { SidebarNavGroups } from './SidebarNavGroups';

export interface SidebarProps {
  /** Bo'limlar (rol bo'yicha filtrlangan, badge'lar bilan) — `adminNavGroups()`. */
  groups: readonly NavGroup[];
  userName: string;
  userRole: string;
  /** Logo bosilganda boriladigan yo'l (rol uy sahifasi). */
  homePath: string;
  onLogout: () => void;
  logoutPending?: boolean;
  /** ❓ Mobil drawer holati. */
  open?: boolean;
  onClose?: (() => void) | undefined;
  /** Qidiruv yorlig'i (`/`, Ctrl/Cmd+K) — mobil rejimda drawer'ni ochish. */
  onSearchShortcut?: (() => void) | undefined;
}

/** Chap dark panel: brend + qidiruv + bo'limlarga ajratilgan nav + user bloki (SPEC-TOKENS 4.10). */
export function Sidebar({
  groups,
  userName,
  userRole,
  homePath,
  onLogout,
  logoutPending = false,
  open = false,
  onClose,
  onSearchShortcut,
}: SidebarProps) {
  return (
    <aside id="app-sidebar" className={styles.sidebar} data-open={open || undefined}>
      <div className={styles.brand}>
        <Link to={homePath} className={styles.logo}>
          Amaliyotchi
        </Link>
        <div className={styles.sub}>Amaliyot nazorati</div>
        {onClose && (
          <button type="button" className={styles.close} onClick={onClose} aria-label="Yopish">
            ×
          </button>
        )}
      </div>
      <SidebarNavGroups groups={groups} onNavigate={onClose} onSearchShortcut={onSearchShortcut} />
      <div className={styles.user}>
        <Avatar name={userName} variant="sidebar" />
        <div className={styles.userText}>
          <div className={styles.name} title={userName}>
            {userName}
          </div>
          <div className={styles.role} title={userRole}>
            {userRole}
          </div>
        </div>
        {/* Ikkinchi qator (matn ustuni ostida) — rol matni kesilmasin (250px sidebar). */}
        <Button
          variant="ghost-dark"
          className={styles.logout}
          onClick={onLogout}
          disabled={logoutPending}
        >
          Chiqish
        </Button>
      </div>
    </aside>
  );
}

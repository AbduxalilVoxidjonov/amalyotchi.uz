import type { CSSProperties, HTMLAttributes, MouseEvent, ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Avatar, cn } from '@amaliyotchi/shared/ui';
import styles from './DataTable.module.css';

export interface DataTableColumn<T> {
  /** Ustun kaliti (React key + `renderCell` uchun). */
  key: string;
  header: ReactNode;
  /** Grid kengligi: `'1.6fr'`, `'120px'`, `'minmax(90px,1fr)'`. Default `1fr`. */
  width?: string;
  align?: 'left' | 'right';
  /** Katak uslubi (SPEC-TOKENS 4.4 `plain`): mono · strong · dim · wrap. */
  mono?: boolean;
  strong?: boolean;
  dim?: boolean;
  wrap?: boolean;
  /** Katak mazmuni. Berilmasa `row[key]` string sifatida chiqadi. */
  render?: (row: T, index: number) => ReactNode;
}

export interface DataTableProps<T> extends Omit<HTMLAttributes<HTMLDivElement>, 'children'> {
  columns: readonly DataTableColumn<T>[];
  rows: readonly T[];
  /** Qator kaliti (id). */
  rowKey: (row: T, index: number) => string | number;
  /** comfortable (13px) · compact (8px) — SPEC-TOKENS 4.19. */
  density?: 'comfortable' | 'compact';
  /** Qator oxirida amallar ustuni (Ko'rish, Tasdiqlash...). */
  actions?: (row: T, index: number) => ReactNode;
  /** `actions` ustuni kengligi (default `max-content` — eng keng qator tugmalari bo'yicha, hamma qatorda bir xil). */
  actionsWidth?: string;
  /** Jadval tepasi (qidiruv, pill'lar). */
  toolbar?: ReactNode;
  /** Pastki polosa (pagination). */
  footer?: ReactNode;
  /** Bo'sh holat matni (default "Ma'lumot yo'q"). */
  emptyText?: ReactNode;
  /** Gorizontal scroll'da minimal kenglik. */
  minWidth?: string;
  onRowClick?: (row: T, index: number) => void;
  /**
   * Qatorning istalgan joyi bosilsa — shu href'ga o'tiladi (faqat sichqoncha uchun qulaylik;
   * klaviatura uchun tab to'xtash joyi qator ichidagi nom havolasi bo'lib qoladi).
   * `undefined` qaytarsa, o'sha qator bosilmaydi. Interaktiv elementlar
   * (`ROW_CLICK_IGNORE_SELECTOR`) va `[data-row-click-ignore]` bosilishi navigatsiya qilmaydi.
   * Cmd/Ctrl+bosish yoki o'rta tugma — yangi tab. Router konteksti talab qilinadi.
   */
  rowHref?: (row: T) => string | undefined;
  selectedKey?: string | number | null;
  /** `true` qaytarsa qator xiralashtiriladi (masalan `isActive: false` yozuv). */
  rowDim?: (row: T, index: number) => boolean;
  /** a11y: jadval nomi. */
  'aria-label'?: string;
}

function defaultCell<T>(row: T, key: string): ReactNode {
  const v = (row as Record<string, unknown>)[key];
  if (v === null || v === undefined) return '—';
  return typeof v === 'object' ? String(v) : (v as ReactNode);
}

/** Qator bosilishi e'tiborsiz qoldiriladigan (o'z vazifasi bor) elementlar. */
const ROW_CLICK_IGNORE_SELECTOR = [
  'a',
  'button',
  'input',
  'select',
  'textarea',
  'label',
  'summary',
  '[role=button]',
  '[role=menuitem]',
  '[role=checkbox]',
  '[contenteditable]',
  '[data-row-click-ignore]',
].join(',');

/** Bosish qator navigatsiyasini ishga tushirmasligi kerakmi (interaktiv element, belgilangan matn...). */
function shouldIgnoreRowClick(e: MouseEvent<HTMLElement>): boolean {
  if (e.defaultPrevented) return true;
  const row = e.currentTarget;
  const target = e.target;
  // Portal (modal/menyu) ichidan React orqali ko'tarilgan hodisalar — qator DOM'ida emas.
  if (!(target instanceof Element) || !row.contains(target)) return true;
  const hit = target.closest(ROW_CLICK_IGNORE_SELECTOR);
  if (hit && hit !== row && row.contains(hit)) return true;
  const selection = typeof window !== 'undefined' ? window.getSelection()?.toString() : '';
  return Boolean(selection);
}

type RowNavigate = (href: string) => void;

/**
 * CSS grid asosidagi jadval (SPEC-TOKENS 4.4). Ustunlar `width` orqali `grid-template-columns` ga yig'iladi.
 * Butun jadval bitta grid: head/qatorlar `display: contents` — shuning uchun `auto`/`max-content`
 * kenglikli ustunlar (masalan `actions`) ham barcha qatorlarda bir xil kenglikda bo'ladi.
 */
export function DataTable<T>(props: DataTableProps<T>) {
  // `useNavigate` faqat `rowHref` bo'lganda chaqiriladi — router'siz ishlatilgan jadvallar o'zgarmaydi.
  return props.rowHref ? <RoutedDataTable {...props} /> : <DataTableView {...props} />;
}

function RoutedDataTable<T>(props: DataTableProps<T>) {
  const navigate = useNavigate();
  return <DataTableView {...props} navigate={navigate} />;
}

function DataTableView<T>({
  columns,
  rows,
  rowKey,
  density = 'comfortable',
  actions,
  actionsWidth = 'max-content',
  toolbar,
  footer,
  emptyText = "Ma'lumot yo'q",
  minWidth,
  onRowClick,
  selectedKey = null,
  rowDim,
  rowHref,
  navigate,
  className,
  style,
  ...rest
}: DataTableProps<T> & { navigate?: RowNavigate }) {
  const cols = columns.map((c) => c.width ?? '1fr').concat(actions ? [actionsWidth] : []);
  const gridStyle = {
    ...style,
    '--cols': cols.join(' '),
    '--min-w': minWidth,
  } as CSSProperties;

  return (
    <div
      className={cn(styles.table, className)}
      data-density={density}
      role="table"
      style={gridStyle}
      {...rest}
    >
      {toolbar && <div className={styles.toolbar}>{toolbar}</div>}
      <div className={styles.scroll}>
        <div className={styles.grid} role="rowgroup">
          <div className={styles.head} role="row">
            {columns.map((c) => (
              <div key={c.key} role="columnheader" className={styles.headCell} data-align={c.align}>
                {c.header}
              </div>
            ))}
            {actions && (
              <div
                role="columnheader"
                className={styles.headCell}
                data-align="right"
                aria-label="Amallar"
              />
            )}
          </div>
          {rows.length === 0 ? (
            <div className={styles.empty} role="row">
              <div role="cell" className={styles.emptyCell}>
                {emptyText}
              </div>
            </div>
          ) : (
            rows.map((row, i) => {
              const key = rowKey(row, i);
              const clickable = Boolean(onRowClick);
              const href = navigate ? rowHref?.(row) : undefined;
              const openHref = (e: MouseEvent<HTMLDivElement>, newTab: boolean) => {
                if (!href || shouldIgnoreRowClick(e)) return;
                if (newTab) window.open(href, '_blank', 'noopener');
                else navigate?.(href);
              };
              const handleClick =
                clickable || href
                  ? (e: MouseEvent<HTMLDivElement>) => {
                      onRowClick?.(row, i);
                      if (e.button === 0) openHref(e, e.metaKey || e.ctrlKey);
                    }
                  : undefined;
              return (
                <div
                  key={key}
                  className={styles.row}
                  role="row"
                  data-clickable={clickable || undefined}
                  data-selected={selectedKey !== null && selectedKey === key ? 'true' : undefined}
                  data-row-dim={rowDim?.(row, i) ? 'true' : undefined}
                  data-row-link={href ? 'true' : undefined}
                  onClick={handleClick}
                  onAuxClick={href ? (e) => e.button === 1 && openHref(e, true) : undefined}
                >
                  {columns.map((c, ci) => (
                    <div
                      key={c.key}
                      role="cell"
                      className={styles.cell}
                      // Qator `display: contents` — fokus ololmaydi; klaviatura uchun birinchi katak fokuslanadi.
                      tabIndex={clickable && ci === 0 ? 0 : undefined}
                      onKeyDown={
                        clickable && ci === 0
                          ? (e) => {
                              if (
                                e.target === e.currentTarget &&
                                (e.key === 'Enter' || e.key === ' ')
                              ) {
                                e.preventDefault();
                                onRowClick?.(row, i);
                              }
                            }
                          : undefined
                      }
                      data-align={c.align}
                      data-mono={c.mono || undefined}
                      data-strong={c.strong || undefined}
                      data-dim={c.dim || undefined}
                      data-wrap={c.wrap || undefined}
                    >
                      <div className={styles.cellText}>
                        {c.render ? c.render(row, i) : defaultCell(row, c.key)}
                      </div>
                    </div>
                  ))}
                  {actions && (
                    <div role="cell" className={cn(styles.cell, styles.actions)}>
                      <div className={styles.cellText}>{actions(row, i)}</div>
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>
      </div>
      {footer && <div className={styles.footer}>{footer}</div>}
    </div>
  );
}

export interface PersonCellProps {
  name: string;
  /** Mono ikkilamchi (guruh, HEMIS ID). */
  sub?: ReactNode;
  /** Berilsa — ism havola bo'ladi (masalan talaba profiliga: `/admin/students/{id}`). */
  to?: string;
}

/** Avatar + ism katagi (Bugun / Talabalar jadvali). `to` bilan ism "ichiga kiradigan" havola. */
export function PersonCell({ name, sub, to }: PersonCellProps) {
  return (
    <div className={styles.person}>
      <Avatar name={name} variant="table" />
      <div className={styles.personText}>
        <div className={styles.personName}>
          {to ? (
            <Link className={styles.personLink} to={to}>
              {name}
            </Link>
          ) : (
            name
          )}
        </div>
        {sub && <div className={styles.personSub}>{sub}</div>}
      </div>
    </div>
  );
}

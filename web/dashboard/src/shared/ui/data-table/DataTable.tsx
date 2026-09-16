import type { CSSProperties, HTMLAttributes, ReactNode } from 'react';
import { Link } from 'react-router-dom';
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

/**
 * CSS grid asosidagi jadval (SPEC-TOKENS 4.4). Ustunlar `width` orqali `grid-template-columns` ga yig'iladi.
 * Butun jadval bitta grid: head/qatorlar `display: contents` — shuning uchun `auto`/`max-content`
 * kenglikli ustunlar (masalan `actions`) ham barcha qatorlarda bir xil kenglikda bo'ladi.
 */
export function DataTable<T>({
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
  className,
  style,
  ...rest
}: DataTableProps<T>) {
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
              return (
                <div
                  key={key}
                  className={styles.row}
                  role="row"
                  data-clickable={clickable || undefined}
                  data-selected={selectedKey !== null && selectedKey === key ? 'true' : undefined}
                  data-row-dim={rowDim?.(row, i) ? 'true' : undefined}
                  onClick={clickable ? () => onRowClick?.(row, i) : undefined}
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

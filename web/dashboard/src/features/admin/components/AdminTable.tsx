import type { ReactNode } from 'react';
import { Button, DataTable, EmptyState, Input, type DataTableColumn } from '@/shared/ui';
import type { Paged } from '../shared/types';
import styles from './AdminTable.module.css';
import { ErrorState, LoadingState } from './PageStatus';

/** Container → presentation: so'rov holati + qidiruv/sahifa boshqaruvi (`tableState()` yig'adi). */
export interface TableStateProps<T> {
  data: Paged<T> | undefined;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
  search: string;
  onSearchChange: (value: string) => void;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}

export interface AdminTableProps<T> extends TableStateProps<T> {
  'aria-label': string;
  columns: readonly DataTableColumn<T>[];
  rowKey: (row: T) => string;
  /** Toolbar o'ngidagi tugmalar (SPEC 9.x `actions`). */
  actions?: ReactNode;
  /** Qator amallari (❓ dizaynda yo'q — faqat berilsa chiqadi). */
  rowActions?: ((row: T) => ReactNode) | undefined;
  /** `true` qaytarsa qator xiralashtiriladi (masalan `isActive: false`). */
  rowDim?: ((row: T) => boolean) | undefined;
  emptyTitle: string;
  emptyDescription?: ReactNode;
  minWidth?: string;
}

/**
 * Admin jadval ekranlari uchun umumiy presentation (SPEC-SCREENS §9 `isTable`):
 * toolbar (qidiruv `Qidirish…` 230px + actions) → head → rows → footer.
 * ❓ Dizaynda footer/pagination yo'q — minimal "1–20 / 1284" + Oldingi/Keyingi.
 * Loading / error / empty holatlari jadval ichida (chegara bitta).
 */
export function AdminTable<T>({
  'aria-label': ariaLabel,
  columns,
  rowKey,
  actions,
  rowActions,
  rowDim,
  emptyTitle,
  emptyDescription,
  minWidth,
  data,
  isLoading,
  error,
  onRetry,
  search,
  onSearchChange,
  page,
  pageSize,
  onPageChange,
}: AdminTableProps<T>) {
  const rows = !isLoading && !error && data ? data.items : [];
  const total = data?.total ?? 0;
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  const state: ReactNode = isLoading ? (
    <LoadingState />
  ) : error ? (
    <ErrorState inline error={error} onRetry={onRetry} />
  ) : (
    <EmptyState
      tone="plain"
      className={styles.empty}
      title={emptyTitle}
      description={
        emptyDescription ?? (search ? "Qidiruv bo'yicha hech narsa topilmadi." : undefined)
      }
    />
  );

  return (
    <DataTable
      aria-label={ariaLabel}
      aria-busy={isLoading || undefined}
      columns={columns}
      rows={rows}
      rowKey={(r) => rowKey(r)}
      {...(rowActions ? { actions: rowActions } : {})}
      {...(rowDim ? { rowDim: (r: T) => rowDim(r) } : {})}
      {...(minWidth ? { minWidth } : {})}
      emptyText={state}
      toolbar={
        <>
          <Input
            variant="search"
            type="search"
            placeholder="Qidirish…"
            aria-label="Qidirish"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
          />
          {actions && <div className={styles.actions}>{actions}</div>}
        </>
      }
      footer={
        <>
          <span>
            {from}–{to} / {total}
          </span>
          <span className={styles.pager}>
            <Button size="xs" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
              Oldingi
            </Button>
            <Button size="xs" disabled={to >= total} onClick={() => onPageChange(page + 1)}>
              Keyingi
            </Button>
          </span>
        </>
      }
    />
  );
}

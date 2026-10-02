import type { ReactNode } from 'react';
import { Button, DataTable, EmptyState, type DataTableColumn } from '@/shared/ui';
import { ErrorState, LoadingState } from '../../components/PageStatus';
import { RowLink } from '../../components/RowLink';
import { formatCount, formatDateTime } from '../../shared/format';
import type { Paged } from '../../shared/types';
import { messageHref } from '../api';
import { shortText } from '../format';
import type { MessageSummary } from '../types';
import { DeliveryProgress } from './DeliveryProgress';
import { MessageStatusBadge } from './MessageStatusBadge';
import styles from './Messages.module.css';

const COLUMNS: DataTableColumn<MessageSummary>[] = [
  {
    key: 'createdAt',
    header: 'Sana',
    width: 'minmax(120px,.7fr)',
    mono: true,
    dim: true,
    render: (m) => formatDateTime(m.createdAt),
  },
  {
    key: 'text',
    header: 'Xabar',
    width: 'minmax(220px,2fr)',
    strong: true,
    render: (m) => (
      <span className={styles.twoLine}>
        <RowLink to={messageHref(m.id)}>{shortText(m.text)}</RowLink>
        <span className={styles.subLine}>
          {m.audienceLabel} · {m.createdByName}
        </span>
      </span>
    ),
  },
  {
    key: 'progress',
    header: 'Yetkazildi',
    width: 'minmax(130px,.9fr)',
    render: (m) => <DeliveryProgress message={m} />,
  },
  {
    key: 'failed',
    header: 'Xato',
    width: '64px',
    mono: true,
    align: 'right',
    render: (m) =>
      m.failed > 0 ? <span className={styles.bad}>{formatCount(m.failed)}</span> : '0',
  },
  {
    key: 'blocked',
    header: 'Bloklangan',
    width: '96px',
    mono: true,
    align: 'right',
    render: (m) => formatCount(m.blocked),
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(120px,.7fr)',
    render: (m) => <MessageStatusBadge status={m.status} />,
  },
];

export interface SentMessagesTableProps {
  data: Paged<MessageSummary> | undefined;
  isLoading: boolean;
  error: unknown;
  onRetry: () => void;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}

/** "Yuborilganlar" ro'yxati (qidiruvsiz — backend `?page&pageSize` ni qabul qiladi). */
export function SentMessagesTable({
  data,
  isLoading,
  error,
  onRetry,
  page,
  pageSize,
  onPageChange,
}: SentMessagesTableProps) {
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
      title="Hali xabar yuborilmagan"
      description="«Ulanganlar» bo'limida talabalarni tanlab, birinchi xabarni yuboring."
    />
  );

  return (
    <DataTable
      aria-label="Yuborilgan xabarlar"
      aria-busy={isLoading || undefined}
      columns={COLUMNS}
      rows={rows}
      rowKey={(m) => m.id}
      rowHref={(m) => messageHref(m.id)}
      minWidth="900px"
      emptyText={state}
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

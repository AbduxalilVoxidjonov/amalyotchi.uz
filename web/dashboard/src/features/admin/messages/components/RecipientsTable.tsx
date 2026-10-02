import { useMemo, type ReactNode } from 'react';
import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { SELECT_COLUMN_WIDTH, SelectBox, SelectCell } from '../../components/SelectBox';
import { DASH, formatDateTime } from '../../shared/format';
import { PageSizeInput } from '../../students/components/PageSizeInput';
import type { MessageRecipientRow } from '../types';
import styles from './Messages.module.css';

export const BOT_BLOCKED_HINT = 'Talaba botni bloklagan yoki ishga tushirmagan — xabar yetmaydi';

/** "412-22 · 3-kurs" + fakultet (ikki qator). Ma'lumot yo'q — "—". */
function scopeCell(r: MessageRecipientRow): ReactNode {
  const top = [r.groupName, r.course ? `${r.course}-kurs` : null].filter(Boolean).join(' · ');
  if (!top && !r.facultyName) return <span data-dim>{DASH}</span>;
  return (
    <span className={styles.twoLine}>
      <span>{top || DASH}</span>
      {r.facultyName && <span className={styles.subLine}>{r.facultyName}</span>}
    </span>
  );
}

function statusCell(r: MessageRecipientRow): ReactNode {
  if (!r.botBlocked) return <Badge status="ok">Ulangan</Badge>;
  return (
    <Badge status="bad" title={BOT_BLOCKED_HINT}>
      Bot bloklangan
      <span className={styles.srOnly}> — {BOT_BLOCKED_HINT}</span>
    </Badge>
  );
}

const BASE_COLUMNS: DataTableColumn<MessageRecipientRow>[] = [
  { key: 'fullName', header: 'FISH', width: 'minmax(180px,1.5fr)', strong: true },
  {
    key: 'hemisId',
    header: 'HEMIS ID',
    width: 'minmax(90px,.7fr)',
    mono: true,
    dim: true,
    render: (r) => r.hemisId ?? DASH,
  },
  {
    key: 'telegramUserId',
    header: 'Telegram ID',
    width: 'minmax(110px,.8fr)',
    mono: true,
    dim: true,
    render: (r) => String(r.telegramUserId),
  },
  {
    key: 'scope',
    header: 'Guruh / fakultet',
    width: 'minmax(170px,1.3fr)',
    render: scopeCell,
  },
  {
    key: 'telegramLinkedAt',
    header: 'Ulangan',
    width: 'minmax(120px,.8fr)',
    mono: true,
    dim: true,
    render: (r) => formatDateTime(r.telegramLinkedAt),
  },
  { key: 'status', header: 'Holat', width: 'minmax(130px,.8fr)', render: statusCell },
];

/** Tanlov (Set yoki Map — faqat `has`/`size` kerak). */
export type SelectionLike = Pick<ReadonlySet<string>, 'has' | 'size'>;

export interface RecipientsTableProps extends TableStateProps<MessageRecipientRow> {
  selectedIds: SelectionLike;
  onToggleRow: (row: MessageRecipientRow, checked: boolean) => void;
  /** Sahifadagi barcha qatorlarni belgilash/bekor qilish. */
  onToggleAll: (checked: boolean) => void;
  onClearSelection: () => void;
  /** Bitta talabaga yozish (qator amali). */
  onWriteOne: (row: MessageRecipientRow) => void;
  onWriteSelected: () => void;
  /** Ommaviy xabar (joriy filtr bo'yicha yoki hammaga). */
  onWriteBulk: () => void;
  filters?: ReactNode;
  onPageSizeChange: (size: number) => void;
  emptyDescription?: ReactNode;
}

/** "Ulanganlar" jadvali: belgilash, qator amali "Xabar yozish", yuqorida ommaviy amallar. */
export function RecipientsTable({
  selectedIds,
  onToggleRow,
  onToggleAll,
  onClearSelection,
  onWriteOne,
  onWriteSelected,
  onWriteBulk,
  filters,
  onPageSizeChange,
  emptyDescription,
  ...state
}: RecipientsTableProps) {
  const rows = state.data?.items ?? [];
  const total = state.data?.total ?? 0;
  const selectedOnPage = rows.filter((r) => selectedIds.has(r.userId)).length;
  const allChecked = rows.length > 0 && selectedOnPage === rows.length;
  const someChecked = selectedOnPage > 0 && !allChecked;
  const selectedCount = selectedIds.size;

  const columns = useMemo<DataTableColumn<MessageRecipientRow>[]>(
    () => [
      {
        key: 'select',
        header: (
          <SelectBox
            checked={allChecked}
            indeterminate={someChecked}
            label="Sahifadagilarni tanlash"
            onChange={onToggleAll}
          />
        ),
        width: SELECT_COLUMN_WIDTH,
        render: (r) => (
          <SelectCell
            checked={selectedIds.has(r.userId)}
            label={`${r.fullName} ni tanlash`}
            onChange={(checked) => onToggleRow(r, checked)}
          />
        ),
      },
      ...BASE_COLUMNS,
    ],
    [allChecked, someChecked, selectedIds, onToggleAll, onToggleRow],
  );

  return (
    <AdminTable
      aria-label="Telegram ulangan talabalar"
      columns={columns}
      rowKey={(r) => r.userId}
      minWidth="1020px"
      emptyTitle="Telegram ulangan talaba yo'q"
      {...(emptyDescription ? { emptyDescription } : {})}
      filters={filters}
      rowActions={(r) => (
        <Button
          size="xs"
          onClick={() => onWriteOne(r)}
          aria-label={`${r.fullName} ga xabar yozish`}
        >
          Xabar yozish
        </Button>
      )}
      footerControls={<PageSizeInput value={state.pageSize} onChange={onPageSizeChange} />}
      actions={
        <>
          {selectedCount > 0 && (
            <Button size="xs" onClick={onClearSelection}>
              Tanlovni tozalash
            </Button>
          )}
          <Button
            size="xs"
            variant="primary"
            disabled={selectedCount === 0}
            onClick={onWriteSelected}
          >
            Tanlanganlarga yozish ({selectedCount})
          </Button>
          <Button
            size="xs"
            disabled={state.isLoading || !!state.error || total === 0}
            onClick={onWriteBulk}
          >
            Ommaviy xabar
          </Button>
        </>
      }
      {...state}
    />
  );
}

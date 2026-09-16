import { AuthFileButton } from '@/shared/files';
import { Badge, Button, DataTable, Input, type DataTableColumn } from '@/shared/ui';
import { fmtDayRange } from '../../format';
import { LEAVE_STATUS_LABEL, type LeaveDecision, type LeaveRequest } from '../types';

/** "14.10" · "15.10–16.10" */
const formatLeaveDates = (r: LeaveRequest) => fmtDayRange(r.dateFrom, r.dateTo);

/** SPEC-SCREENS §9.1 — cols/head aynan. */
const COLUMNS: DataTableColumn<LeaveRequest>[] = [
  { key: 'studentName', header: 'Talaba', width: 'minmax(170px,1.5fr)', strong: true },
  { key: 'group', header: 'Guruh', width: 'minmax(90px,.6fr)', mono: true, dim: true },
  {
    key: 'dates',
    header: 'Sana(lar)',
    width: 'minmax(120px,1fr)',
    mono: true,
    render: formatLeaveDates,
  },
  { key: 'reason', header: 'Sabab', width: 'minmax(220px,2fr)' },
  {
    key: 'document',
    header: 'Hujjat',
    width: 'minmax(110px,.9fr)',
    mono: true,
    dim: true,
    render: (r) =>
      r.document ? (
        r.document.url ? (
          <AuthFileButton url={r.document.url} name={r.document.name} variant="link" />
        ) : (
          r.document.name
        )
      ) : (
        '—'
      ),
  },
  {
    key: 'status',
    header: 'Qaror',
    width: 'minmax(150px,1.2fr)',
    render: (r) => {
      const s = LEAVE_STATUS_LABEL[r.status];
      return (
        <Badge status={s.kind} title={r.comment ?? undefined}>
          {s.label}
        </Badge>
      );
    },
  },
];

export interface LeaveRequestsTableProps {
  rows: readonly LeaveRequest[];
  search: string;
  onSearch: (q: string) => void;
  pendingIds: ReadonlySet<string>;
  onDecide: (ids: string[], decision: LeaveDecision) => void;
  onApproveAll: () => void;
  canApproveAll: boolean;
  /** Qaror xatosi (masalan 409 — allaqachon hal qilingan). */
  error?: string | undefined;
}

export function LeaveRequestsTable({
  rows,
  search,
  onSearch,
  pendingIds,
  onDecide,
  onApproveAll,
  canApproveAll,
  error,
}: LeaveRequestsTableProps) {
  return (
    <DataTable
      aria-label="Ruxsat so'rovlari"
      columns={COLUMNS}
      rows={rows}
      rowKey={(r) => r.id}
      minWidth="900px"
      // Tugmasiz (qaror qilingan) qatorlarda ham ustun kengligi bir xil qolsin.
      actionsWidth="minmax(170px,max-content)"
      emptyText="Ruxsat so'rovlari yo'q"
      footer={error && <span role="alert">{error}</span>}
      toolbar={
        <>
          <Input
            variant="search"
            placeholder="Qidirish…"
            aria-label="Qidirish"
            value={search}
            onChange={(e) => onSearch(e.target.value)}
          />
          <Button size="sm" disabled={!canApproveAll} onClick={onApproveAll}>
            Hammasini tasdiqlash
          </Button>
        </>
      }
      // ❓ Dizaynda qator tugmalari yo'q — qaror endpoint'i uchun qo'shildi (faqat Kutilmoqda).
      actions={(r) =>
        r.status === 'pending' ? (
          <>
            <Button
              size="xs"
              disabled={pendingIds.has(r.id)}
              onClick={() => onDecide([r.id], 'approve')}
            >
              Tasdiqlash
            </Button>
            <Button
              size="xs"
              variant="danger"
              disabled={pendingIds.has(r.id)}
              onClick={() => onDecide([r.id], 'reject')}
            >
              Rad etish
            </Button>
          </>
        ) : null
      }
    />
  );
}

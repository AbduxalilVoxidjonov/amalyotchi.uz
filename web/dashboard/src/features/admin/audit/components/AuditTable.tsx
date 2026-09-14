import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { formatShortDateTime } from '../../shared/format';
import { AUDIT_ACTION_LABEL, auditDetail, auditWho } from '../describe';
import type { AuditEntry } from '../types';

/** SPEC §9.8 ustunlari. Tafsilot va "Kim" — v2 xom maydonlardan (`describe.ts`). */
const COLUMNS: DataTableColumn<AuditEntry>[] = [
  {
    key: 'at',
    header: 'Vaqt',
    width: 'minmax(120px,1.1fr)',
    mono: true,
    dim: true,
    render: (r) => <time dateTime={r.at}>{formatShortDateTime(r.at)}</time>,
  },
  {
    key: 'action',
    header: 'Amal',
    width: 'minmax(160px,1.2fr)',
    render: (r) => {
      const a = AUDIT_ACTION_LABEL[r.action] ?? { label: r.action, kind: 'neu' as const };
      return <Badge status={a.kind}>{a.label}</Badge>;
    },
  },
  { key: 'detail', header: 'Tafsilot', width: 'minmax(280px,2.6fr)', render: auditDetail },
  { key: 'who', header: 'Kim', width: 'minmax(160px,1.2fr)', dim: true, render: auditWho },
];

export interface AuditTableProps extends TableStateProps<AuditEntry> {
  onExport: () => void;
  onFilter: () => void;
}

export function AuditTable({ onExport, onFilter, ...state }: AuditTableProps) {
  return (
    <AdminTable
      aria-label="Audit jurnali"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="760px"
      emptyTitle="Audit yozuvlari yo'q"
      actions={
        <>
          <Button size="xs" onClick={onExport}>
            Excel
          </Button>
          <Button size="xs" onClick={onFilter}>
            Filtr: barcha amallar
          </Button>
        </>
      }
      {...state}
    />
  );
}

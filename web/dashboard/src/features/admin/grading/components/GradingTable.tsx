import { Badge, Button, DataTable, Input, type DataTableColumn } from '@/shared/ui';
import { fmtDecimal } from '../../format';
import { gradeLabel, type GradingRow } from '../types';
import styles from './GradingTable.module.css';

/** Butun ballar (tyutor 0–20, tavsifnoma 0–10) — kasrsiz. */
const dash = (v: number | null) => (v === null ? '—' : String(v));

/** SPEC-SCREENS §9.2 — cols/head aynan. */
const COLUMNS: DataTableColumn<GradingRow>[] = [
  { key: 'name', header: 'Talaba', width: 'minmax(180px,1.6fr)', strong: true },
  {
    key: 'attendance',
    header: 'Davomat · 40',
    width: 'minmax(130px,1.1fr)',
    mono: true,
    render: (r) => `${fmtDecimal(r.attendance.points)} · ${Math.round(r.attendance.pct)}%`,
  },
  {
    key: 'reports',
    header: 'Hisobot · 30',
    width: 'minmax(130px,1.1fr)',
    mono: true,
    render: (r) => `${fmtDecimal(r.reports.points)} · ${fmtDecimal(r.reports.avg)}`,
  },
  {
    key: 'tutorPoints',
    header: 'Tyutor · 20',
    width: 'minmax(100px,.9fr)',
    mono: true,
    render: (r) => dash(r.tutorPoints),
  },
  {
    key: 'referencePoints',
    header: 'Tavsifnoma · 10',
    width: 'minmax(130px,.9fr)',
    mono: true,
    render: (r) => dash(r.referencePoints),
  },
  {
    key: 'total',
    header: 'Jami',
    width: 'minmax(80px,.8fr)',
    mono: true,
    strong: true,
    render: (r) => fmtDecimal(r.total),
  },
  {
    key: 'grade',
    header: 'Baho',
    width: 'minmax(160px,.9fr)',
    render: (r) => {
      const g = gradeLabel(r);
      return <Badge status={g.kind}>{g.label}</Badge>;
    },
  },
];

export interface GradingTableProps {
  rows: readonly GradingRow[];
  search: string;
  onSearch: (q: string) => void;
  onAcceptRecommended: () => void;
  canAccept: boolean;
  error?: string | undefined;
}

export function GradingTable({
  rows,
  search,
  onSearch,
  onAcceptRecommended,
  canAccept,
  error,
}: GradingTableProps) {
  return (
    <DataTable
      aria-label="Baholash"
      columns={COLUMNS}
      rows={rows}
      rowKey={(r) => r.studentId}
      minWidth="920px"
      emptyText="Baholanadigan talabalar yo'q"
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
          <span className={styles.actions}>
            <Button size="sm" disabled={!canAccept} onClick={onAcceptRecommended}>
              Tavsiya etilgan ballarni qabul qilish
            </Button>
            {/* TODO: Excel eksport — handler dizaynda yo'q ❓ */}
            <Button size="sm">Excel</Button>
          </span>
        </>
      }
    />
  );
}

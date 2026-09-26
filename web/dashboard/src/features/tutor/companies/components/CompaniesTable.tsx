import { Badge, DataTable, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { fmtDistance, fmtTin } from '../../format';
import { COMPANY_FLAG_LABEL, studentsOfLimit, type TutorCompany } from '../types';
import styles from './CompaniesTable.module.css';

/**
 * Tyutor · Korxonalar jadvali. "Aktiv talaba" ustuni ko'lamdagi / jami AKTIV amaliyotchilar
 * (hozir davom etayotgan davr, tasdiqlangan ariza) nisbatini beradi;
 * `overLimit` bo'lsa STIR chegarasi ogohlantirishi (jami/chegara) qo'shiladi.
 */
const COLUMNS: DataTableColumn<TutorCompany>[] = [
  { key: 'name', header: 'Korxona', width: 'minmax(190px,1.7fr)', strong: true },
  {
    key: 'tin',
    header: 'STIR',
    width: 'minmax(120px,1fr)',
    mono: true,
    dim: true,
    render: (r) => fmtTin(r.tin),
  },
  { key: 'address', header: 'Manzil', width: 'minmax(180px,1.5fr)', wrap: true },
  {
    key: 'radiusM',
    header: 'Radius',
    width: 'minmax(90px,.7fr)',
    mono: true,
    render: (r) => fmtDistance(r.radiusM),
  },
  {
    key: 'students',
    header: (
      <span title="Hozir shu korxonada aktiv amaliyot o'tayotgan talabalar: ko'lamingizda / jami">
        Aktiv talaba
      </span>
    ),
    width: 'minmax(120px,1fr)',
    render: (r) => (
      <div className={styles.students}>
        <span className={styles.ratio}>
          {r.students} / {r.totalStudents}
        </span>
        {r.overLimit ? (
          <Badge status="bad" size="sm" title={`STIR chegarasi: ${r.maxStudents} aktiv talaba`}>
            {studentsOfLimit(r.totalStudents, r.maxStudents)}
          </Badge>
        ) : (
          <span className={styles.note}>ko'lamda / jami</span>
        )}
      </div>
    ),
  },
  {
    key: 'attendancePct',
    header: 'Davomat',
    width: 'minmax(130px,1.2fr)',
    render: (r) => <ProgressBar value={r.attendancePct} label={`${r.name} o'rtacha davomati`} />,
  },
  {
    key: 'suspiciousDays',
    header: 'Shubhali',
    width: 'minmax(90px,.7fr)',
    mono: true,
    render: (r) => (r.suspiciousDays === 0 ? '—' : `${r.suspiciousDays} kun`),
  },
  {
    key: 'flag',
    header: 'Belgi',
    width: 'minmax(150px,1fr)',
    dim: true,
    render: (r) =>
      r.flag ? (
        <Badge status={COMPANY_FLAG_LABEL[r.flag].kind}>{COMPANY_FLAG_LABEL[r.flag].label}</Badge>
      ) : (
        '—'
      ),
  },
];

export interface CompaniesTableProps {
  rows: readonly TutorCompany[];
  onOpen: (company: TutorCompany) => void;
}

export function CompaniesTable({ rows, onOpen }: CompaniesTableProps) {
  return (
    <DataTable
      aria-label="Korxonalar"
      columns={COLUMNS}
      rows={rows}
      rowKey={(r) => r.id}
      minWidth="980px"
      onRowClick={(r) => onOpen(r)}
      emptyText="Ko'lamingizda korxona yo'q"
    />
  );
}

import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { RowLink } from '../../components/RowLink';
import { formatTin } from '../../shared/format';
import { COMPANY_FLAG_LABEL, studentsOfLimit, type Company } from '../types';

/**
 * SPEC §9.7 ustunlari. STIR — xom 9 raqamdan formatlanadi; korxona nomi detail sahifasiga havola
 * (`AdminTable` qator bosishni uzatmaydi — ierarxiya jadvallaridagi `RowLink` naqshi).
 * `overLimit` qatorda talaba ustuni "21/10" ogohlantirish `Badge`iga aylanadi.
 */
const COLUMNS: DataTableColumn<Company>[] = [
  {
    key: 'name',
    header: 'Korxona',
    width: 'minmax(190px,1.7fr)',
    strong: true,
    render: (r) => <RowLink to={`/admin/companies/${r.id}`}>{r.name}</RowLink>,
  },
  {
    key: 'tin',
    header: 'STIR',
    width: 'minmax(120px,1fr)',
    mono: true,
    dim: true,
    render: (r) => formatTin(r.tin),
  },
  { key: 'address', header: 'Manzil', width: 'minmax(200px,1.7fr)' },
  {
    key: 'radiusM',
    header: 'Radius',
    width: 'minmax(90px,.8fr)',
    mono: true,
    render: (r) => `${r.radiusM} m`,
  },
  {
    key: 'students',
    header: 'Talaba',
    width: 'minmax(110px,.9fr)',
    mono: true,
    render: (r) =>
      r.overLimit ? (
        <Badge status="bad" title={`STIR chegarasi: ${r.maxStudents} talaba`}>
          {studentsOfLimit(r.students, r.maxStudents)}
        </Badge>
      ) : (
        String(r.students)
      ),
  },
  {
    key: 'flag',
    header: 'Belgi',
    width: 'minmax(170px,1fr)',
    dim: true,
    render: (r) =>
      r.flag ? (
        <Badge status={COMPANY_FLAG_LABEL[r.flag].kind}>{COMPANY_FLAG_LABEL[r.flag].label}</Badge>
      ) : (
        '—'
      ),
  },
];

export interface CompaniesTableProps extends TableStateProps<Company> {
  onExport: () => void;
  onShowSuspicious: () => void;
}

export function CompaniesTable({ onExport, onShowSuspicious, ...state }: CompaniesTableProps) {
  return (
    <AdminTable
      aria-label="Korxonalar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="880px"
      emptyTitle="Korxonalar yo'q"
      rowDim={(r) => !r.isActive}
      actions={
        <>
          <Button size="xs" onClick={onExport}>
            Excel
          </Button>
          <Button size="xs" onClick={onShowSuspicious}>
            Shubhali to'planishlar
          </Button>
        </>
      }
      {...state}
    />
  );
}

import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { formatTin } from '../../shared/format';
import { COMPANY_FLAG_LABEL, type Company } from '../types';

/** SPEC §9.7 ustunlari. STIR — xom 9 raqamdan formatlanadi. */
const COLUMNS: DataTableColumn<Company>[] = [
  { key: 'name', header: 'Korxona', width: 'minmax(190px,1.7fr)', strong: true },
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
  { key: 'students', header: 'Talaba', width: 'minmax(80px,.8fr)', mono: true },
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

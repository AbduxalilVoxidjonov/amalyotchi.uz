import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { RowLink } from '../../components/RowLink';
import { formatTin } from '../../shared/format';
import { COMPANY_FLAG_LABEL, studentsOfLimit, type Company } from '../types';
import styles from './CompaniesTable.module.css';

/** Korxona detail sahifasi — nom havolasi va butun qator bosilishi uchun bitta manba. */
const companyHref = (r: Company) => `/admin/companies/${r.id}`;

/**
 * SPEC §9.7 ustunlari. STIR — xom 9 raqamdan formatlanadi; korxona nomi detail sahifasiga havola
 * (`RowLink` — klaviatura uchun; sichqoncha bilan qatorning istalgan joyi ham shu sahifani ochadi).
 * `overLimit` qatorda talaba ustuni "21/10" ogohlantirish `Badge`iga aylanadi.
 */
const COLUMNS: DataTableColumn<Company>[] = [
  {
    key: 'name',
    header: 'Korxona',
    width: 'minmax(190px,1.7fr)',
    strong: true,
    render: (r) => <RowLink to={companyHref(r)}>{r.name}</RowLink>,
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
  onCreate: () => void;
  /** Shablonni yuklab olish (`GET .../import/template`). */
  onDownloadTemplate: () => void;
  templateLoading?: boolean;
  /** Shablon yuklanmasa — tugmalar yonidagi xabar. */
  templateError?: string | null;
  onImportExcel: () => void;
  onEdit: (company: Company) => void;
  onToggleStatus: (company: Company) => void;
  onDelete: (company: Company) => void;
}

export function CompaniesTable({
  onCreate,
  onDownloadTemplate,
  templateLoading = false,
  templateError = null,
  onImportExcel,
  onEdit,
  onToggleStatus,
  onDelete,
  ...state
}: CompaniesTableProps) {
  return (
    <AdminTable
      aria-label="Korxonalar"
      columns={COLUMNS}
      rowKey={(r) => r.id}
      minWidth="880px"
      emptyTitle="Korxonalar yo'q"
      rowDim={(r) => !r.isActive}
      rowHref={companyHref}
      rowActions={(r) => (
        <>
          <Button size="xs" onClick={() => onEdit(r)}>
            Tahrirlash
          </Button>
          <Button size="xs" onClick={() => onToggleStatus(r)}>
            {r.isActive ? 'Faolsizlantirish' : 'Faollashtirish'}
          </Button>
          <Button size="xs" variant="danger" onClick={() => onDelete(r)}>
            O'chirish
          </Button>
        </>
      )}
      actions={
        <>
          {templateError && (
            <span role="alert" className={styles.error}>
              {templateError}
            </span>
          )}
          <Button size="xs" variant="primary" onClick={onCreate}>
            Yangi korxona
          </Button>
          <Button size="xs" onClick={onDownloadTemplate} disabled={templateLoading}>
            {templateLoading ? 'Tayyorlanmoqda…' : 'Shablon'}
          </Button>
          <Button size="xs" onClick={onImportExcel}>
            Excel import
          </Button>
        </>
      }
      {...state}
    />
  );
}

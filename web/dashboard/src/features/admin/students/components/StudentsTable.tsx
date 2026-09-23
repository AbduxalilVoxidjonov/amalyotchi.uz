import { useEffect, useMemo, useRef } from 'react';
import { Badge, Button, Checkbox, ProgressBar, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { RowLink } from '../../components/RowLink';
import { STUDENT_STATUS_LABEL, type Student } from '../types';
import styles from './StudentsTable.module.css';

/** Belgilash katagi: matnli label yo'q (jadval ustuni), nom `aria-label` orqali beriladi. */
function SelectBox({
  checked,
  indeterminate = false,
  label,
  onChange,
}: {
  checked: boolean;
  indeterminate?: boolean;
  label: string;
  onChange: (checked: boolean) => void;
}) {
  const ref = useRef<HTMLInputElement>(null);
  // `indeterminate` — faqat DOM xossasi, atribut orqali berib bo'lmaydi.
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate;
  }, [indeterminate]);

  return (
    <Checkbox
      ref={ref}
      wrapperClassName={styles.check}
      label=""
      aria-label={label}
      checked={checked}
      onChange={(e) => onChange(e.target.checked)}
    />
  );
}

/** SPEC §9.6 ustunlari. ❓ `bar:0` — dizaynda foiz bo'sh; matn o'rni (38px) saqlanadi — track'lar teng. */
const BASE_COLUMNS: DataTableColumn<Student>[] = [
  {
    key: 'fullName',
    header: 'Talaba',
    width: 'minmax(180px,1.6fr)',
    strong: true,
    // Ism — talaba profiliga havola (`/admin/students/:studentId`), tyutor/korxona jadvallaridagidek.
    render: (r) => <RowLink to={`/admin/students/${r.id}`}>{r.fullName}</RowLink>,
  },
  { key: 'group', header: 'Guruh', width: 'minmax(90px,.7fr)', mono: true, dim: true },
  { key: 'faculty', header: 'Fakultet', width: 'minmax(170px,1.3fr)' },
  {
    key: 'company',
    header: 'Korxona',
    width: 'minmax(160px,1.4fr)',
    render: (r) => r.company ?? <span data-dim>—</span>,
  },
  {
    key: 'attendancePct',
    header: 'Davomat',
    width: 'minmax(140px,1fr)',
    render: (r) => (
      <ProgressBar
        value={r.attendancePct}
        showValue={r.attendancePct > 0 ? true : 'blank'}
        label={`${r.fullName} davomati`}
      />
    ),
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(140px,.9fr)',
    render: (r) => (
      <Badge status={STUDENT_STATUS_LABEL[r.status].kind}>
        {STUDENT_STATUS_LABEL[r.status].label}
      </Badge>
    ),
  },
];

export interface StudentsTableProps extends TableStateProps<Student> {
  /** Shablonni yuklab olish (`GET .../import/template`). */
  onDownloadTemplate: () => void;
  templateLoading?: boolean;
  /** Shablon yuklanmasa — tugmalar yonidagi xabar. */
  templateError?: string | null;
  onImportExcel: () => void;
  /** Belgilangan talabalar (ommaviy biriktirish uchun). */
  selectedIds: ReadonlySet<string>;
  onToggleRow: (id: string, checked: boolean) => void;
  /** Sahifadagi barcha qatorlarni belgilash/bekor qilish. */
  onToggleAll: (checked: boolean) => void;
  onAssignCompany: () => void;
}

export function StudentsTable({
  onDownloadTemplate,
  templateLoading = false,
  templateError = null,
  onImportExcel,
  selectedIds,
  onToggleRow,
  onToggleAll,
  onAssignCompany,
  ...state
}: StudentsTableProps) {
  const rows = state.data?.items ?? [];
  const { page, pageSize } = state;

  const selectedOnPage = rows.filter((r) => selectedIds.has(r.id)).length;
  const allChecked = rows.length > 0 && selectedOnPage === rows.length;
  const someChecked = selectedOnPage > 0 && !allChecked;

  const columns = useMemo<DataTableColumn<Student>[]>(
    () => [
      {
        key: 'select',
        header: (
          <SelectBox
            checked={allChecked}
            indeterminate={someChecked}
            label="Hammasini belgilash"
            onChange={onToggleAll}
          />
        ),
        // Katak padding'i: chapda `--row-pad-x` (birinchi ustun), o'ngda 12px (`--col-gap`).
        // Trek = padding + 15px katakcha — aks holda kontent qutisi katakchadan tor bo'lib,
        // qatorlarda u `cellText` ning `overflow: hidden` i bilan kesiladi.
        width: 'calc(var(--row-pad-x) + 15px + 12px)',
        render: (r) => (
          <SelectBox
            checked={selectedIds.has(r.id)}
            label={`${r.fullName} ni belgilash`}
            onChange={(checked) => onToggleRow(r.id, checked)}
          />
        ),
      },
      {
        key: 'index',
        header: '№',
        width: '48px',
        mono: true,
        dim: true,
        align: 'right',
        // Tartib raqami sahifani hisobga oladi (2-sahifada 21, 22, ...).
        render: (_r, i) => (page - 1) * pageSize + i + 1,
      },
      ...BASE_COLUMNS,
    ],
    [allChecked, someChecked, selectedIds, onToggleAll, onToggleRow, page, pageSize],
  );

  const selectedCount = selectedIds.size;

  return (
    <AdminTable
      aria-label="Talabalar"
      columns={columns}
      rowKey={(r) => r.id}
      minWidth="980px"
      emptyTitle="Talabalar yo'q"
      actions={
        <>
          {selectedCount > 0 && (
            <>
              <span className={styles.selected} aria-live="polite">
                {selectedCount} ta tanlandi
              </span>
              <Button size="xs" variant="primary" onClick={onAssignCompany}>
                Korxonaga biriktirish
              </Button>
            </>
          )}
          {templateError && (
            <span role="alert" className={styles.error}>
              {templateError}
            </span>
          )}
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

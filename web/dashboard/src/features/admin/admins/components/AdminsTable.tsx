import { Badge, Button, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { DASH, formatDateTime, formatPhone } from '../../shared/format';
import { blockDisabledReason } from '../rules';
import type { AdminAccount } from '../types';
import styles from '../Admins.module.css';

function columns(currentUserId: string | undefined): DataTableColumn<AdminAccount>[] {
  return [
    {
      key: 'fullName',
      header: 'FISH',
      width: 'minmax(180px,1.5fr)',
      strong: true,
      render: (r) => (
        <span className={styles.name}>
          {r.fullName}
          {r.isSuperAdmin && <Badge status="info">Bosh admin</Badge>}
          {r.id === currentUserId && <span className={styles.self}>(siz)</span>}
        </span>
      ),
    },
    { key: 'login', header: 'Login', width: 'minmax(120px,1fr)', mono: true },
    {
      key: 'phoneNumber',
      header: 'Telefon',
      width: 'minmax(150px,1fr)',
      mono: true,
      dim: true,
      render: (r) => formatPhone(r.phoneNumber),
    },
    {
      key: 'lastLoginAt',
      header: 'Oxirgi kirish',
      width: 'minmax(140px,1fr)',
      mono: true,
      dim: true,
      render: (r) => (r.lastLoginAt ? formatDateTime(r.lastLoginAt) : DASH),
    },
    {
      key: 'isActive',
      header: 'Holat',
      width: 'minmax(110px,.8fr)',
      render: (r) =>
        r.isActive ? <Badge status="ok">Faol</Badge> : <Badge status="bad">Bloklangan</Badge>,
    },
  ];
}

export interface AdminsTableProps extends TableStateProps<AdminAccount> {
  currentUserId: string | undefined;
  onCreate: () => void;
  onEdit: (admin: AdminAccount) => void;
  onToggleStatus: (admin: AdminAccount) => void;
  onResetPassword: (admin: AdminAccount) => void;
}

/** "Adminlar" jadvali — qidiruv, qator amallari va "Yangi admin" (admin jadvallari naqshida). */
export function AdminsTable({
  currentUserId,
  onCreate,
  onEdit,
  onToggleStatus,
  onResetPassword,
  ...state
}: AdminsTableProps) {
  return (
    <AdminTable
      aria-label="Adminlar"
      columns={columns(currentUserId)}
      rowKey={(r) => r.id}
      minWidth="860px"
      emptyTitle="Adminlar yo'q"
      rowDim={(r) => !r.isActive}
      rowActions={(r) => {
        const blockReason = r.isActive ? blockDisabledReason(r, currentUserId) : null;
        return (
          <>
            <Button size="xs" onClick={() => onEdit(r)} aria-label={`${r.fullName} — tahrirlash`}>
              Tahrirlash
            </Button>
            <Button
              size="xs"
              onClick={() => onResetPassword(r)}
              aria-label={`${r.fullName} — parolni tiklash`}
            >
              Parolni tiklash
            </Button>
            <Button
              size="xs"
              onClick={() => onToggleStatus(r)}
              disabled={blockReason !== null}
              title={blockReason ?? undefined}
              aria-label={`${r.fullName} — ${r.isActive ? 'bloklash' : 'faollashtirish'}`}
            >
              {r.isActive ? 'Bloklash' : 'Faollashtirish'}
            </Button>
          </>
        );
      }}
      actions={
        <Button size="xs" onClick={onCreate}>
          Yangi admin
        </Button>
      }
      {...state}
    />
  );
}

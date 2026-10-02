import { Badge, Select, type DataTableColumn } from '@/shared/ui';
import { AdminTable, type TableStateProps } from '../../components/AdminTable';
import { DASH, formatDateTime } from '../../shared/format';
import { DELIVERY_STATUS_LABEL, type DeliveryStatus, type MessageDeliveryRow } from '../types';
import styles from './Messages.module.css';

const STATUS_OPTIONS: DeliveryStatus[] = ['sent', 'failed', 'blocked', 'pending'];

const COLUMNS: DataTableColumn<MessageDeliveryRow>[] = [
  { key: 'fullName', header: 'FISH', width: 'minmax(180px,1.4fr)', strong: true },
  {
    key: 'hemisId',
    header: 'HEMIS ID',
    width: 'minmax(90px,.7fr)',
    mono: true,
    dim: true,
    render: (d) => d.hemisId ?? DASH,
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(110px,.7fr)',
    render: (d) => (
      <Badge status={DELIVERY_STATUS_LABEL[d.status].kind}>
        {DELIVERY_STATUS_LABEL[d.status].label}
      </Badge>
    ),
  },
  {
    key: 'sentAt',
    header: 'Yetkazilgan vaqt',
    width: 'minmax(130px,.8fr)',
    mono: true,
    dim: true,
    render: (d) => formatDateTime(d.sentAt),
  },
  {
    key: 'error',
    header: 'Xato',
    width: 'minmax(200px,1.6fr)',
    wrap: true,
    render: (d) => (d.error ? <span className={styles.errorText}>{d.error}</span> : DASH),
  },
];

export interface DeliveriesTableProps extends TableStateProps<MessageDeliveryRow> {
  status: DeliveryStatus | '';
  onStatusChange: (status: DeliveryStatus | '') => void;
}

/** Tafsilot sahifasidagi yetkazishlar jadvali: holat filtri + qidiruv + sahifalash. */
export function DeliveriesTable({ status, onStatusChange, ...state }: DeliveriesTableProps) {
  return (
    <AdminTable
      aria-label="Yetkazishlar"
      columns={COLUMNS}
      rowKey={(d) => d.userId}
      minWidth="820px"
      emptyTitle="Yetkazish topilmadi"
      {...(status && !state.search
        ? { emptyDescription: "Tanlangan holat bo'yicha yozuv yo'q." }
        : {})}
      filters={
        <Select
          aria-label="Holat"
          variant="search"
          wrapperClassName={styles.statusFilter}
          value={status}
          onChange={(e) => onStatusChange(e.target.value as DeliveryStatus | '')}
        >
          <option value="">Holat: barchasi</option>
          {STATUS_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {DELIVERY_STATUS_LABEL[s].label}
            </option>
          ))}
        </Select>
      }
      {...state}
    />
  );
}

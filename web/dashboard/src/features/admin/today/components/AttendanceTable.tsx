import type { ReactNode } from 'react';
import { Badge, Button, DataTable, PersonCell, type DataTableColumn } from '@/shared/ui';
import { locationTrackHref } from '../../location/paths';
import { fmtDistance } from '../../shared/fmt';
import { DIARY_LABEL, rowStatusLabel, type AttendanceRow, type Paged } from '../types';
import styles from './AttendanceTable.module.css';
import { PresenceCell } from './PresenceCell';

/**
 * SPEC-SCREENS §3 — 9 ustunli (+ "Joyida", LIVE-JOYLASHUV.md) davomat jadvali. Katak ranglari (JS `inFg/outFg/diaryFg/distFg`)
 * `data-tone` orqali: late · empty · ok · pending · out · muted.
 */
function Tone({ tone, children }: { tone: string; children: ReactNode }) {
  return (
    <span className={styles.tone} data-tone={tone}>
      {children}
    </span>
  );
}

/** Kunlik joylashuv izi — faqat bugun check-in qilgan (yoki kuzatuv holati bor) talabalar uchun. */
function trackHref(r: AttendanceRow): string | undefined {
  return r.checkIn !== null || r.presence
    ? locationTrackHref(r.studentId, { name: r.name })
    : undefined;
}

const COLUMNS: DataTableColumn<AttendanceRow>[] = [
  {
    key: 'name',
    header: 'Talaba',
    width: '1.7fr',
    render: (r) => {
      const to = trackHref(r);
      return to ? <PersonCell name={r.name} to={to} /> : <PersonCell name={r.name} />;
    },
  },
  { key: 'group', header: 'Guruh', width: '.6fr', mono: true, dim: true },
  {
    key: 'company',
    header: 'Korxona',
    width: '1.4fr',
    render: (r) => <span className={styles.company}>{r.company ?? '—'}</span>,
  },
  {
    key: 'checkIn',
    header: 'Check-in',
    width: '.7fr',
    mono: true,
    render: (r) => (
      <Tone tone={r.checkIn === null ? 'empty' : r.status === 'late' ? 'late' : 'text'}>
        {r.checkIn ?? '—'}
      </Tone>
    ),
  },
  {
    key: 'checkOut',
    header: 'Check-out',
    width: '.7fr',
    mono: true,
    render: (r) => <Tone tone={r.checkOut === null ? 'empty' : 'text'}>{r.checkOut ?? '—'}</Tone>,
  },
  {
    key: 'diary',
    header: 'Kundalik',
    width: '.8fr',
    render: (r) => (
      <Tone tone={r.diary === 'written' ? 'ok' : r.diary === 'pending' ? 'late' : 'empty'}>
        {r.diary ? DIARY_LABEL[r.diary] : '—'}
      </Tone>
    ),
  },
  {
    key: 'distance',
    header: 'Masofa',
    width: '.7fr',
    mono: true,
    render: (r) => (
      <Tone tone={r.distanceM === null ? 'empty' : r.outOfRadius ? 'out' : 'muted'}>
        {r.distanceM === null ? '—' : fmtDistance(r.distanceM)}
      </Tone>
    ),
  },
  {
    key: 'presence',
    header: 'Joyida',
    width: '1.1fr',
    render: (r) => <PresenceCell presence={r.presence} />,
  },
  {
    key: 'status',
    header: 'Status',
    width: '.9fr',
    render: (r) => {
      const s = rowStatusLabel(r);
      const hint = [r.manual && "qo'lda belgilangan", r.autoClosed && 'avtomatik yopilgan']
        .filter(Boolean)
        .join(' · ');
      return (
        <Badge status={s.kind} title={hint || undefined}>
          {s.label}
        </Badge>
      );
    },
  },
];

export interface AttendanceTableProps {
  rows: readonly AttendanceRow[];
  pagination: Pick<Paged<unknown>, 'page' | 'pageSize' | 'total'>;
  toolbar: ReactNode;
  onPageChange: (page: number) => void;
}

export function AttendanceTable({ rows, pagination, toolbar, onPageChange }: AttendanceTableProps) {
  const { page, pageSize, total } = pagination;
  const shown = rows.length;
  const hasPrev = page > 1;
  const hasNext = page * pageSize < total;
  return (
    <DataTable
      aria-label="Bugungi davomat"
      columns={COLUMNS}
      rows={rows}
      rowKey={(r) => r.studentId}
      rowHref={trackHref}
      minWidth="980px"
      toolbar={toolbar}
      emptyText="Bu filtr bo'yicha talabalar yo'q"
      footer={
        <>
          <span>
            {total} talabadan {shown} tasi ko'rsatilgan
          </span>
          <span className={styles.pager}>
            <Button size="xs" disabled={!hasPrev} onClick={() => onPageChange(page - 1)}>
              Oldingi
            </Button>
            <Button size="xs" disabled={!hasNext} onClick={() => onPageChange(page + 1)}>
              Keyingi
            </Button>
          </span>
        </>
      }
    />
  );
}

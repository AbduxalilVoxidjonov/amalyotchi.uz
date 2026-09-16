import { useState } from 'react';
import {
  Badge,
  Chip,
  ChipRow,
  DataTable,
  Eyebrow,
  MapPlaceholder,
  type DataTableColumn,
} from '@/shared/ui';
import { fmtDateOnly, fmtDayMonth, fmtDistance } from '../../format';
import { DIARY_STATUS_LABEL } from '../../diaries/types';
import { dayStatusLabel, fmtCoords, type StudentAttendanceDay } from '../types';
import { PhotoPreview } from './PhotoPreview';
import styles from './AttendanceDayTable.module.css';

const WEEK_DAYS = ['Ya', 'Du', 'Se', 'Ch', 'Pa', 'Ju', 'Sh'] as const;

function weekDay(date: string): string {
  const d = new Date(`${date}T00:00:00Z`).getUTCDay();
  return WEEK_DAYS[d] ?? '';
}

/**
 * Kun belgilari. "Shubhali" holati `Badge` da ko'rinadi, shuning uchun bu yerda takrorlanmaydi.
 * Radius tashqarisi (QABUL QILINGAN belgilanish radiusdan uzoq) va rad etilgan urinishlar —
 * ikki xil signal, alohida ko'rsatiladi.
 */
function flags(day: StudentAttendanceDay): string[] {
  const out: string[] = [];
  if (day.checkIn?.outOfRadius) out.push('Radius tashqarisida');
  if (day.manual) out.push("Qo'lda kiritilgan");
  if (day.autoClosed) out.push('Avto yopilgan');
  if (day.rejectedAttempts > 0) out.push(`${day.rejectedAttempts} urinish rad etildi`);
  if (day.leaveRequestId) out.push("Ruxsat so'rovi");
  return out;
}

const COLUMNS: DataTableColumn<StudentAttendanceDay>[] = [
  {
    key: 'date',
    header: 'Sana',
    width: 'minmax(110px, .9fr)',
    mono: true,
    render: (d) => (
      <span>
        {fmtDateOnly(d.date)} <span className={styles.dim}>{weekDay(d.date)}</span>
      </span>
    ),
  },
  {
    key: 'status',
    header: 'Holat',
    width: 'minmax(100px, .8fr)',
    render: (d) => {
      const s = dayStatusLabel(d);
      return <Badge status={s.kind}>{s.label}</Badge>;
    },
  },
  {
    key: 'checkIn',
    header: 'Kirish',
    width: 'minmax(80px, .6fr)',
    mono: true,
    render: (d) => d.checkIn?.at ?? '—',
  },
  {
    key: 'checkOut',
    header: 'Chiqish',
    width: 'minmax(80px, .6fr)',
    mono: true,
    render: (d) => d.checkOut?.at ?? '—',
  },
  {
    key: 'distance',
    header: 'Masofa',
    width: 'minmax(90px, .6fr)',
    mono: true,
    render: (d) =>
      d.checkIn?.distanceM === null || d.checkIn === null ? (
        '—'
      ) : (
        <span data-out-of-radius={d.checkIn.outOfRadius || undefined} className={styles.distance}>
          {fmtDistance(d.checkIn.distanceM)}
        </span>
      ),
  },
  {
    key: 'location',
    header: 'Lokatsiya',
    width: 'minmax(150px, 1fr)',
    mono: true,
    dim: true,
    render: (d) => fmtCoords(d.checkIn?.lat ?? null, d.checkIn?.lng ?? null) ?? '—',
  },
  {
    key: 'photo',
    header: 'Rasm',
    width: 'minmax(70px, 70px)',
    render: (d) =>
      d.checkIn?.photoUrl ? (
        <PhotoPreview url={d.checkIn.photoUrl} label={`${fmtDateOnly(d.date)} check-in rasmi`} />
      ) : (
        <span className={styles.dim}>—</span>
      ),
  },
  {
    key: 'flags',
    header: 'Belgilar',
    width: 'minmax(170px, 1.2fr)',
    wrap: true,
    render: (d) => {
      const items = flags(d);
      if (items.length === 0) return <span className={styles.dim}>—</span>;
      return (
        <ChipRow>
          {items.map((f) => (
            <Chip key={f} variant="fmt">
              {f}
            </Chip>
          ))}
        </ChipRow>
      );
    },
  },
  {
    key: 'diary',
    header: 'Kundalik',
    width: 'minmax(120px, .9fr)',
    render: (d) => {
      if (!d.diary) return <span className={styles.dim}>—</span>;
      const s = DIARY_STATUS_LABEL[d.diary.status];
      return (
        <span className={styles.diary}>
          <Badge status={s.kind}>{s.label}</Badge>
          {d.diary.score !== null && <span className={styles.score}>{d.diary.score}</span>}
        </span>
      );
    },
  },
];

export interface AttendanceDayTableProps {
  days: readonly StudentAttendanceDay[];
  /** Korxona geofence radiusi (kun panelida ko'rsatiladi). */
  radiusM: number | null;
}

/** Kun-bakun davomat jadvali (KONTRAKT §2.2). Qator bosilsa — kun tafsiloti paneli ochiladi. */
export function AttendanceDayTable({ days, radiusM }: AttendanceDayTableProps) {
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const selected = days.find((d) => d.date === selectedDate) ?? null;

  return (
    <>
      <DataTable
        aria-label="Kun-bakun davomat"
        columns={COLUMNS}
        rows={days}
        rowKey={(d) => d.date}
        density="compact"
        minWidth="1080px"
        selectedKey={selectedDate}
        onRowClick={(d) => setSelectedDate((prev) => (prev === d.date ? null : d.date))}
        emptyText="Tanlangan oraliqda davomat yozuvi yo'q"
      />

      {selected && (
        <section
          className={styles.panel}
          aria-label={`${fmtDateOnly(selected.date)} kuni tafsiloti`}
        >
          <div className={styles.panelMain}>
            <Eyebrow margin="none">{fmtDateOnly(selected.date)} — kun tafsiloti</Eyebrow>
            <dl className={styles.panelFacts}>
              <div>
                <dt>Urinishlar</dt>
                <dd>
                  {selected.attempts} ta
                  {selected.rejectedAttempts > 0 && ` · ${selected.rejectedAttempts} rad etilgan`}
                </dd>
              </div>
              <div>
                <dt>Ish kuni</dt>
                <dd>{selected.isWorkDay ? 'Ha' : "Yo'q"}</dd>
              </div>
              {radiusM !== null && (
                <div>
                  <dt>Radius</dt>
                  <dd>{fmtDistance(radiusM)}</dd>
                </div>
              )}
            </dl>
            {selected.suspiciousReason && (
              <p className={styles.reason}>{selected.suspiciousReason}</p>
            )}
            {selected.manualReason && <p className={styles.reason}>{selected.manualReason}</p>}
          </div>
          <MapPlaceholder
            title={`Belgilanish nuqtasi · ${fmtDayMonth(selected.date)}`}
            coords={
              fmtCoords(selected.checkIn?.lat ?? null, selected.checkIn?.lng ?? null) ??
              'Koordinata yo‘q'
            }
            note={
              selected.checkIn?.accuracyM != null
                ? `Aniqlik: ${fmtDistance(selected.checkIn.accuracyM)}`
                : undefined
            }
            height={150}
          />
          {selected.checkIn?.photoUrl && (
            <div className={styles.panelPhoto}>
              <Eyebrow margin="none">Check-in rasmi</Eyebrow>
              <PhotoPreview
                url={selected.checkIn.photoUrl}
                label={`${fmtDateOnly(selected.date)} check-in rasmi`}
                size="card"
              />
            </div>
          )}
        </section>
      )}
    </>
  );
}

import { useId, useState } from 'react';
import {
  Badge,
  Chip,
  ChipRow,
  DataTable,
  Eyebrow,
  Modal,
  type DataTableColumn,
} from '@/shared/ui';
import { errorMessage } from '@/shared/api';
import { fmtDateOnly, fmtDistance } from '../../format';
import { useDiaryReview } from '../../diaries/hooks';
import { DIARY_STATUS_LABEL, type DiaryEntry } from '../../diaries/types';
import {
  dayStatusLabel,
  fmtCoords,
  type AttendancePunch,
  type StudentApiArea,
  type StudentAttendanceDay,
} from '../types';
import { PhotoPreview } from './PhotoPreview';
import { DayFilesPanel } from './DayFilesPanel';
import { DiaryDayCard } from './DiaryDayCard';
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
  /** Talabaning kundaliklari — kun oynasida o'sha kunga tegishlisi ochiladi. */
  diaries?: readonly DiaryEntry[];
  /** Kun oynasidagi baholash qaysi rol endpoint'iga borishi (tyutor yoki admin paneli). */
  area?: StudentApiArea;
}

/** Bitta belgilanish (kirish/chiqish) — ixcham ustun: sarlavha (vaqt), masofa/aniqlik,
 * koordinata (bitta qator) va selfi (kichik thumbnail — bosilsa katta preview). */
function PunchBlock({
  kind,
  punch,
  date,
}: {
  kind: 'in' | 'out';
  punch: AttendancePunch | null;
  date: string;
}) {
  const headingId = useId();
  const title = kind === 'in' ? 'Kirish' : 'Chiqish';

  if (!punch) {
    return (
      <div className={styles.punch} role="group" aria-labelledby={headingId}>
        <Eyebrow margin="none" id={headingId}>
          {title}
        </Eyebrow>
        <p className={styles.punchEmpty}>Belgilanmagan</p>
      </div>
    );
  }

  const coords = fmtCoords(punch.lat, punch.lng);
  const photoLabel = `${fmtDateOnly(date)} ${kind === 'in' ? 'check-in' : 'check-out'} rasmi`;

  return (
    <div className={styles.punch} role="group" aria-labelledby={headingId}>
      <Eyebrow margin="none" id={headingId}>
        {title} · {punch.at}
      </Eyebrow>
      <div className={styles.punchBody}>
        <dl className={styles.punchFacts}>
          <div>
            <dt>Masofa</dt>
            <dd data-out-of-radius={punch.outOfRadius || undefined} className={styles.distance}>
              {punch.distanceM === null ? '—' : fmtDistance(punch.distanceM)}
              {punch.outOfRadius && <span className={styles.outNote}> · radius tashqarisida</span>}
            </dd>
          </div>
          <div>
            <dt>Aniqlik</dt>
            <dd>{punch.accuracyM === null ? '—' : fmtDistance(punch.accuracyM)}</dd>
          </div>
          <div>
            <dt>Lokatsiya</dt>
            <dd className={styles.coords}>{coords ?? 'Yuborilmagan'}</dd>
          </div>
        </dl>
        {punch.photoUrl ? (
          <figure className={styles.selfie}>
            <PhotoPreview url={punch.photoUrl} label={photoLabel} />
            <figcaption className={styles.selfieCaption}>{title} selfisi</figcaption>
          </figure>
        ) : (
          <p className={styles.noSelfie}>Selfi yo'q</p>
        )}
      </div>
    </div>
  );
}

/**
 * Kundalik jadval (KONTRAKT §2.2) — har bir amaliyot kuni bir qator.
 * Sana bosilsa, o'sha kun ixcham **oynada** (modal, ~640px) ochiladi: tepada kirish va chiqish
 * yonma-yon (vaqt, masofa, aniqlik, koordinata, selfi thumbnail), ostida kun sanoqlari bir qatorda
 * (urinishlar · ish kuni · radius), oxirida kundalik (qisqartirilgan matn, baholash, fayllar ro'yxati).
 */
export function AttendanceDayTable({
  days,
  radiusM,
  diaries = [],
  area = 'tutor',
}: AttendanceDayTableProps) {
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const selected = days.find((d) => d.date === selectedDate) ?? null;
  const diary = selected ? (diaries.find((e) => e.date === selected.date) ?? null) : null;
  const status = selected ? dayStatusLabel(selected) : null;
  const review = useDiaryReview(area);

  const diaryHeadingId = useId();

  return (
    <>
      <DataTable
        aria-label="Kundalik jadval"
        columns={COLUMNS}
        rows={days}
        rowKey={(d) => d.date}
        density="compact"
        minWidth="1080px"
        selectedKey={selectedDate}
        onRowClick={(d) => {
          review.reset();
          setSelectedDate(d.date);
        }}
        emptyText="Tanlangan oraliqda davomat yozuvi yo'q"
      />

      <Modal
        open={selected !== null}
        onClose={() => {
          review.reset();
          setSelectedDate(null);
        }}
        width="min(640px, 96vw)"
        title={
          selected && status ? (
            <span className={styles.modalTitle}>
              {fmtDateOnly(selected.date)} — kun tafsiloti
              <Badge status={status.kind}>{status.label}</Badge>
            </span>
          ) : (
            'Kun tafsiloti'
          )
        }
      >
        {selected && (
          <div className={styles.panel}>
            <section className={styles.details} aria-label="Kun ma'lumotlari">
              <div className={styles.punches}>
                <PunchBlock kind="in" punch={selected.checkIn} date={selected.date} />
                <PunchBlock kind="out" punch={selected.checkOut} date={selected.date} />
              </div>

              <dl className={styles.stats}>
                <div>
                  <dt>Urinishlar</dt>
                  <dd>
                    {selected.attempts} ta
                    {selected.rejectedAttempts > 0 &&
                      ` · ${selected.rejectedAttempts} rad etilgan`}
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
            </section>

            <section className={styles.panelDiary} aria-labelledby={diaryHeadingId}>
              <Eyebrow margin="none" id={diaryHeadingId}>
                Shu kunga yuborgan kundaligi
              </Eyebrow>
              {diary ? (
                <>
                  <DiaryDayCard
                    entry={diary}
                    compact
                    showFiles={false}
                    review={{
                      pending: review.isPending,
                      error: review.error ? errorMessage(review.error) : undefined,
                      onReview: (body) => review.mutate({ id: diary.id, body }),
                    }}
                  />
                  <DayFilesPanel attachments={diary.files} />
                </>
              ) : selected.diary ? (
                <p className={styles.punchEmpty}>
                  Kundalik yuborilgan ({DIARY_STATUS_LABEL[selected.diary.status].label}), lekin
                  matni yuklanmadi — sahifani yangilang.
                </p>
              ) : (
                <p className={styles.punchEmpty}>Bu kunga kundalik yuborilmagan.</p>
              )}
            </section>
          </div>
        )}
      </Modal>
    </>
  );
}

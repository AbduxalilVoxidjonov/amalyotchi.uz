import { Badge, FactGrid, type FactItem } from '@/shared/ui';
import { DAY_STATUS } from '@/features/calendar/types';
import { DIARY_STATUS } from '@/features/diary/types';
import type { PeriodDay, PeriodDayDiary } from '../types';
import styles from './DayList.module.css';

const STATUS_TONE: Partial<Record<PeriodDay['status'], FactItem['tone']>> = {
  present: 'ok',
  late: 'late',
  absent: 'bad',
};

/** Kundalik holati: "Tasdiqlangan · 5 ball" yoki "Kundalik yozilmagan". */
export function DiaryLine({ diary }: { diary: PeriodDayDiary | null }) {
  if (!diary) {
    return (
      <p className={styles.diary}>
        <span className={styles.diaryLabel}>Kundalik:</span>
        <span className={styles.muted}>Kundalik yozilmagan</span>
      </p>
    );
  }
  const meta = DIARY_STATUS[diary.status];
  return (
    <p className={styles.diary}>
      <span className={styles.diaryLabel}>Kundalik:</span>
      <Badge status={meta.kind}>{meta.label}</Badge>
      {diary.score !== null && <span className={styles.score}>{diary.score} ball</span>}
    </p>
  );
}

/** Dam olish / bayram kuni paneli. */
export function DayOffDetails({ day }: { day: PeriodDay }) {
  return <p className={styles.note}>Dam olish kuni{day.holiday ? ` — ${day.holiday}` : ''}</p>;
}

/** Kelgusi ish kuni paneli. */
export function FutureDayDetails() {
  return <p className={styles.note}>Kelgusi ish kuni</p>;
}

/** O'tgan ish kuni: kelish/ketish, holat, belgilar, kundalik. */
export function PastDayDetails({ day }: { day: PeriodDay }) {
  const facts: FactItem[] = [
    { k: 'Kelish', v: day.checkInAt ?? '—' },
    { k: 'Ketish', v: day.checkOutAt ?? '—' },
    { k: 'Holat', v: DAY_STATUS[day.status].label, tone: STATUS_TONE[day.status] ?? 'default' },
  ];
  const flags = [
    day.autoClosed && { key: 'auto', label: 'Avtomatik yopilgan', kind: 'late' as const },
    day.suspicious && { key: 'sus', label: 'Shubhali', kind: 'bad' as const },
    day.manual && { key: 'manual', label: "Qo'lda tuzatilgan", kind: 'info' as const },
  ].filter((f) => f !== false);

  return (
    <>
      <FactGrid items={facts} columns={3} />
      {flags.length > 0 && (
        <div className={styles.flags}>
          {flags.map((f) => (
            <Badge key={f.key} status={f.kind} size="sm">
              {f.label}
            </Badge>
          ))}
        </div>
      )}
      <DiaryLine diary={day.diary} />
    </>
  );
}

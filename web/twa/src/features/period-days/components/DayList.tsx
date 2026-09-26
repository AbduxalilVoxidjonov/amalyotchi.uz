import { memo, useCallback, useEffect, useRef, useState, type Ref } from 'react';
import { Badge } from '@/shared/ui';
import { DayStatusChip } from '@/features/day-status/DayStatusChip';
import { DAY_STATUS } from '@/features/day-status/types';
import { formatDayMonth, weekdayName, type PeriodDay } from '../types';
import { DayOffDetails, FutureDayDetails, PastDayDetails } from './DayDetails';
import { TodayPanel } from './TodayPanel';
import styles from './DayList.module.css';

export interface DayListProps {
  days: readonly PeriodDay[];
  /** Server sanasi — shu qator "Bugun" va birinchi yuklanishda yagona ochiq qator. */
  today: string;
  /**
   * Bugungi qatorga birinchi marta scroll qilish kerakmi (sahifa bo'yicha bir marta —
   * davr almashganda qayta scroll qilinmaydi). Scroll bo'lgach `onAutoScrolled` chaqiriladi.
   */
  autoScroll: boolean;
  onAutoScrolled: () => void;
}

/**
 * Davr kunlari — accordion (bir nechtasi bir vaqtda ochiq bo'lishi mumkin). Faqat bugungi kun
 * avtomatik ochiq; panel mazmuni faqat ochilganda render bo'ladi (45+ kun uchun yengil DOM).
 */
export function DayList({ days, today, autoScroll, onAutoScrolled }: DayListProps) {
  const hasToday = days.some((d) => d.date === today);
  const [open, setOpen] = useState<ReadonlySet<string>>(() => new Set(hasToday ? [today] : []));
  const todayRef = useRef<HTMLLIElement>(null);

  useEffect(() => {
    if (!autoScroll) return;
    const el = todayRef.current;
    if (!el) return;
    // Animatsiyasiz (sukut `behavior: 'auto'`); jsdom'da metod yo'q.
    if (typeof el.scrollIntoView === 'function') el.scrollIntoView({ block: 'start' });
    onAutoScrolled();
  }, [autoScroll, onAutoScrolled]);

  const toggle = useCallback((date: string) => {
    setOpen((prev) => {
      const next = new Set(prev);
      if (next.has(date)) next.delete(date);
      else next.add(date);
      return next;
    });
  }, []);

  return (
    <ol className={styles.list} aria-label="Amaliyot kunlari">
      {days.map((day) => (
        <DayRow
          key={day.date}
          ref={day.date === today ? todayRef : undefined}
          day={day}
          isToday={day.date === today}
          past={day.date < today}
          open={open.has(day.date)}
          onToggle={toggle}
        />
      ))}
    </ol>
  );
}

interface DayRowProps {
  day: PeriodDay;
  isToday: boolean;
  past: boolean;
  open: boolean;
  onToggle: (date: string) => void;
  ref?: Ref<HTMLLIElement> | undefined;
}

const DayRow = memo(function DayRow({ day, isToday, past, open, onToggle, ref }: DayRowProps) {
  const buttonId = `day-${day.date}`;
  const panelId = `day-${day.date}-panel`;
  const dateLabel = `${formatDayMonth(day.date)} · ${weekdayName(day.weekday)}`;
  // Ekran o'quvchi uchun toza nom: "24.09 · Payshanba, bugun, Keldi" (vizual qismlar yopishib qolmasin).
  const label = [dateLabel, isToday && 'bugun', day.holiday, DAY_STATUS[day.status].label]
    .filter(Boolean)
    .join(', ');
  return (
    <li
      ref={ref}
      className={styles.row}
      data-open={open || undefined}
      data-today={isToday || undefined}
      data-muted={!day.isWorkDay || undefined}
    >
      <h3 className={styles.heading}>
        <button
          id={buttonId}
          type="button"
          className={styles.trigger}
          aria-expanded={open}
          aria-controls={panelId}
          aria-label={label}
          onClick={() => onToggle(day.date)}
        >
          <span className={styles.when}>
            <span className={styles.date}>{dateLabel}</span>
            {isToday && (
              <Badge status="info" size="sm">
                Bugun
              </Badge>
            )}
            {day.holiday && <span className={styles.holiday}>{day.holiday}</span>}
          </span>
          <DayStatusChip status={day.status} />
          <span className={styles.chevron} aria-hidden="true" />
        </button>
      </h3>
      <div
        id={panelId}
        role="region"
        aria-labelledby={buttonId}
        className={styles.panel}
        hidden={!open}
      >
        {open && <DayPanelContent day={day} isToday={isToday} past={past} />}
      </div>
    </li>
  );
});

function DayPanelContent({ day, isToday, past }: Pick<DayRowProps, 'day' | 'isToday' | 'past'>) {
  if (isToday) return <TodayPanel day={day} />;
  if (!day.isWorkDay) return <DayOffDetails day={day} />;
  if (!past) return <FutureDayDetails />;
  return <PastDayDetails day={day} />;
}

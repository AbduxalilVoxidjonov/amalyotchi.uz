import { useEffect, useState } from 'react';
import { APP_TIME_ZONE } from '@/shared/lib/date';

const WEEKDAYS = ['Yakshanba', 'Dushanba', 'Seshanba', 'Chorshanba', 'Payshanba', 'Juma', 'Shanba'];

const partsFmt = new Intl.DateTimeFormat('en-GB', {
  timeZone: APP_TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  weekday: 'short',
});

const WEEKDAY_INDEX: Record<string, number> = {
  Sun: 0,
  Mon: 1,
  Tue: 2,
  Wed: 3,
  Thu: 4,
  Fri: 5,
  Sat: 6,
};

/** "12.10.2026 · Chorshanba · 09:47" (Asia/Tashkent). */
export function formatClock(date: Date): string {
  const p: Record<string, string> = {};
  for (const part of partsFmt.formatToParts(date)) p[part.type] = part.value;
  const weekday = WEEKDAYS[WEEKDAY_INDEX[p['weekday'] ?? ''] ?? date.getDay()] ?? '';
  return `${p['day']}.${p['month']}.${p['year']} · ${weekday} · ${p['hour']}:${p['minute']}`;
}

/** Har daqiqada yangilanadigan sana/vaqt chipi matni. */
export function useClock(): string {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const tick = () => setNow(new Date());
    const msToNextMinute = 60_000 - (Date.now() % 60_000);
    let interval: ReturnType<typeof setInterval> | undefined;
    const timeout = setTimeout(() => {
      tick();
      interval = setInterval(tick, 60_000);
    }, msToNextMinute);
    return () => {
      clearTimeout(timeout);
      if (interval) clearInterval(interval);
    };
  }, []);
  return formatClock(now);
}

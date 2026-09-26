import { Card, CardHeader, FactGrid } from '@/shared/ui';
import { formatCount } from '../../shared/format';
import { parseWeekdays, WEEKDAYS } from '../../shared/weekdays';
import { calendarDays, formatDays, formatRange } from '../dates';
import type { PracticePeriodDetail } from '../types';
import styles from './PeriodDetail.module.css';

/** Davr ma'lumotlari: sanalar, ish vaqti, ish kunlari, majburiy kunlar, kundalik talabi. */
export function PeriodInfoCard({ period }: { period: PracticePeriodDetail }) {
  const workDays = parseWeekdays(period.workDays);
  return (
    <Card aria-label="Davr ma'lumotlari">
      <CardHeader title="Davr ma'lumotlari" />
      <FactGrid
        variant="detail"
        className={styles.facts}
        items={[
          {
            k: 'Sanalar',
            v: <span className={styles.mono}>{formatRange(period.startDate, period.endDate)}</span>,
          },
          { k: 'Davomiyligi', v: formatDays(calendarDays(period.startDate, period.endDate)) },
          {
            k: 'Ish vaqti',
            v: <span className={styles.mono}>{`${period.dailyStart}–${period.dailyEnd}`}</span>,
          },
          {
            k: 'Ish kunlari',
            v: (
              <ul className={styles.weekdays} aria-label="Ish kunlari">
                {WEEKDAYS.map((d) => (
                  <li
                    key={d.day}
                    className={styles.weekday}
                    data-on={workDays.has(d.day) || undefined}
                    title={d.name}
                    aria-label={`${d.name}: ${workDays.has(d.day) ? 'ish kuni' : 'dam olish'}`}
                  >
                    {d.short}
                  </li>
                ))}
              </ul>
            ),
          },
          { k: 'Majburiy ish kunlari', v: `${period.requiredDays} kun` },
          {
            k: 'Kundalik hisobot majburiy',
            v: period.dailyReportRequired ? 'Ha' : "Yo'q",
          },
          { k: 'Guruhlar', v: formatCount(period.groupsCount) },
          { k: 'Talabalar', v: formatCount(period.studentsCount) },
        ]}
      />
    </Card>
  );
}

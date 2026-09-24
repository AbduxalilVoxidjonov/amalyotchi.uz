import { AppLink, Button, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { CheckinCard } from '@/features/today/components/CheckinCard';
import { useCheckinFlow, useTodayQuery } from '@/features/today/hooks';
import type { PeriodDay, PeriodDayDiary } from '../types';
import { DiaryLine } from './DayDetails';
import styles from './DayList.module.css';

/**
 * Bugungi kun paneli: check-in oqimi (KELDIM/KETDIM, QR → GPS → selfi — `CheckinCard` + `useCheckinFlow`,
 * o'zgarishsiz) va bugungi kundalik holati. Kundalik yozish — `/kundalik` (faqat `AppLink` orqali:
 * Telegram-Android `<a href>` ni tashqi havola deb ushlaydi).
 * Panel yopilsa komponent unmount bo'ladi — boshlangan oqim bekor qilinadi (QR popup yopiladi).
 */
export function TodayPanel({ day }: { day: PeriodDay }) {
  const today = useTodayQuery();
  const flow = useCheckinFlow();

  if (today.isPending) return <LoadingState height={320} />;
  if (today.isError) {
    return (
      <ErrorState description={errorMessage(today.error)} onRetry={() => void today.refetch()} />
    );
  }
  const data = today.data;
  // `period-days` kechikishi mumkin (invalidate) — yuborilgan kundalik `today` dan darhol ko'rinadi.
  const diary: PeriodDayDiary | null =
    day.diary ??
    (data.diary.submittedToday ? { id: 'today', status: 'submitted', score: null } : null);
  const canWrite = diary === null || diary.status === 'rewrite';

  return (
    <div className={styles.today}>
      <CheckinCard today={data} flow={flow} />
      <div className={styles.todayDiary}>
        <DiaryLine diary={diary} />
        {canWrite && (
          <Button asChild variant="primary" radius="md2" block>
            <AppLink to="/kundalik">Kundalik yozish</AppLink>
          </Button>
        )}
      </div>
    </div>
  );
}

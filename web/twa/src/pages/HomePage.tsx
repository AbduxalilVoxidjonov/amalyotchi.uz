import { useCallback, useState } from 'react';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { PeriodPicker } from '@/features/period/components/PeriodPicker';
import { periodPhase } from '@/features/period/types';
import { DayList } from '@/features/period-days/components/DayList';
import { PeriodOverviewCard } from '@/features/period-days/components/PeriodOverviewCard';
import { PeriodResultCard } from '@/features/period-days/components/PeriodResultCard';
import { usePeriodDaysQuery } from '@/features/period-days/hooks';
import { isPeriodEnded } from '@/features/period-days/summary';
import { PeriodGapCard } from '@/features/today/components/PeriodGapCard';
import { useTodayQuery } from '@/features/today/hooks';
import styles from './pages.module.css';

/**
 * SPEC-SCREENS §8 `isTalaba` — Bosh ekran faqat amaliyot davri haqida: davr kartasi (nom, sanalar,
 * holat, o'tgan ish kunlari), bir nechta davr bo'lsa tanlagich va davrning har bir kuni (accordion).
 * Bugungi kun avtomatik ochiq — ichida check-in oqimi va bugungi kundalik holati.
 * Tanlangan davr tugagan bo'lsa (`isPeriodEnded`: yopilgan yoki sanasi o'tgan) — kunlar ro'yxati
 * o'rniga qisqa yig'indi (`PeriodResultCard`: keldi / kech qoldi / kelmadi, davomat foizi).
 */
export function HomePage() {
  // null — sukut davr (server `isDefault`); tanlansa `?periodId=` bilan qayta yuklanadi.
  const [periodId, setPeriodId] = useState<string | null>(null);
  const q = usePeriodDaysQuery(periodId);
  // Bugungi qatorga scroll — sahifa ochilganda faqat bir marta (davr almashganda emas).
  const [scrolled, setScrolled] = useState(false);
  const markScrolled = useCallback(() => setScrolled(true), []);

  if (q.isPending) return <LoadingState height={360} />;
  if (q.isError) {
    return <ErrorState description={errorMessage(q.error)} onRetry={() => void q.refetch()} />;
  }
  const { period, periods, days, today } = q.data;

  if (!period) {
    return (
      <div className={styles.stack}>
        <EmptyState
          title="Amaliyot davri biriktirilmagan"
          description="Davr biriktirilgach bu yerda uning sanalari va har bir kuni ko'rinadi."
        />
      </div>
    );
  }

  return (
    <div className={styles.stack} aria-busy={q.isPlaceholderData || undefined}>
      {periods.length >= 2 && (
        <PeriodPicker
          periods={periods}
          selectedId={periodId ?? period.id}
          onSelect={setPeriodId}
          disabled={q.isFetching}
        />
      )}
      <PeriodOverviewCard period={period} today={today} />
      <PeriodGap />
      {isPeriodEnded(period, today) ? (
        <PeriodResultCard key={period.id} period={period} days={days} />
      ) : (
        <section className={styles.list} aria-labelledby="home-days-title">
          <h2 id="home-days-title" className={styles.sectionTitle}>
            Kunlar
          </h2>
          <DayList
            // Davr almashsa ochiq qatorlar qayta boshlanadi (yana faqat bugungi).
            key={period.id}
            days={days}
            today={today}
            // Faqat sahifa ochilganda yuklangan (sukut) davr uchun: foydalanuvchi davrni o'zi
            // tanlasa (masalan tugagandan faolga o'tsa) scroll qilinmaydi.
            autoScroll={!scrolled && periodId === null}
            onAutoScrolled={markScrolled}
          />
        </section>
      )}
    </div>
  );
}

/**
 * Ikki davr oralig'i (v3.5 §4.6): bugun hech bir davr ichida emas — belgilanish yopiq;
 * kelgusi davr uchun amaliyot joyi arizasi holati/tugmasi. Aks holda hech narsa chizilmaydi.
 */
function PeriodGap() {
  const today = useTodayQuery();
  const data = today.data;
  if (!data?.period) return null;
  const phase = periodPhase(data.period, data.date);
  if (phase !== 'upcoming' && phase !== 'ended') return null;
  return <PeriodGapCard today={data} period={data.period} phase={phase} />;
}

export default HomePage;

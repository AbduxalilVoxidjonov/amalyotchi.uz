import { Card, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { CheckinCard } from '@/features/today/components/CheckinCard';
import { PeriodGapCard } from '@/features/today/components/PeriodGapCard';
import { PlaceSummary } from '@/features/today/components/PlaceSummary';
import { useCheckinFlow, useTodayQuery } from '@/features/today/hooks';
import { DiaryForm } from '@/features/diary/components/DiaryForm';
import { useCreateDiaryEntry, useDiaryQuery } from '@/features/diary/hooks';
import { rewriteFilesFor } from '@/features/diary/types';
import { periodPhase } from '@/features/period/types';
import { PortfolioView } from '@/features/portfolio/components/PortfolioView';
import styles from './pages.module.css';

/**
 * SPEC-SCREENS §8 `isTalaba` — Bosh ekran: check-in + bugungi kundalik + korxona qisqacha,
 * ostida to'liq portfolio (§16). Ikki blok mustaqil query'lar: portfolio yuklanishi yoki xatosi
 * bugungi kartani kutdirmaydi (va aksincha).
 */
export function HomePage() {
  return (
    <div className={styles.stack}>
      <TodaySection />
      <section className={styles.stack} aria-labelledby="home-portfolio-title">
        <h2 id="home-portfolio-title" className={styles.sectionTitle}>
          Portfolio
        </h2>
        <PortfolioView />
      </section>
    </div>
  );
}

/** Bugungi blok: check-in, kundalik, korxona qisqacha (yoki davrlar oralig'i kartasi). */
function TodaySection() {
  const today = useTodayQuery();
  const checkinFlow = useCheckinFlow();
  const createDiary = useCreateDiaryEntry();
  // PDF majburiy bo'lsa — qayta yozilayotgan bugungi yozuvdagi PDF ham hisob (faqat shunda yuklanadi).
  const diaryList = useDiaryQuery({ enabled: today.data?.diary.pdfRequired === true });

  if (today.isPending) return <LoadingState height={320} />;
  if (today.isError) {
    return (
      <ErrorState description={errorMessage(today.error)} onRetry={() => void today.refetch()} />
    );
  }
  const data = today.data;
  // Ikki davr oralig'i (v3.5 §4.6): belgilanish va kundalik yozish yopiq — davr holati ko'rsatiladi.
  // `period = null` ("Faol amaliyot davri yo'q") — avvalgidek CheckinCard `note` bilan.
  const phase = data.period ? periodPhase(data.period, data.date) : null;
  if (data.period && (phase === 'upcoming' || phase === 'ended')) {
    return <PeriodGapCard today={data} period={data.period} phase={phase} />;
  }

  return (
    <>
      <CheckinCard today={data} flow={checkinFlow} />
      <Card padded="lg">
        <DiaryForm
          title="Bugungi kundalik"
          minChars={data.diary.minChars}
          maxFiles={data.diary.maxFiles}
          pdfRequired={data.diary.pdfRequired}
          existingFiles={rewriteFilesFor(diaryList.data, data.date)}
          submittedToday={data.diary.submittedToday}
          pending={createDiary.isPending}
          error={createDiary.error}
          onSubmit={(input) => createDiary.mutateAsync(input)}
        />
        <PlaceSummary place={data.place} />
      </Card>
    </>
  );
}

export default HomePage;

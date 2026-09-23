import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  CardFooter,
  CardHeader,
  EmptyState,
  ErrorState,
  FactGrid,
  LoadingState,
  ProgressBar,
} from '@/shared/ui';
import { errorMessage, isApiError } from '@/shared/api/client';
import { openExternal } from '@/shared/auth/telegram';
import { formatDate, formatDecimal, formatPercent, formatPeriod } from '@/shared/lib/format';
import { PeriodPicker } from '@/features/period/components/PeriodPicker';
import { usePortfolioQuery } from '@/features/portfolio/hooks';
import { scoreLabel } from '@/features/portfolio/types';
import pages from './pages.module.css';
import styles from './PortfolioPage.module.css';

/** SPEC-SCREENS §16 `isPortfolio` — Portfolio. */
export function PortfolioPage() {
  // null — sukut davr (backend `periodId`); tanlansa `?periodId=` bilan qayta yuklanadi.
  const [periodId, setPeriodId] = useState<string | null>(null);
  const q = usePortfolioQuery(periodId);

  if (q.isPending) return <LoadingState height={360} />;
  if (q.isError) {
    if (isApiError(q.error) && q.error.kind === 'not-found') {
      return (
        <EmptyState
          title="Faol amaliyot davri yo'q"
          description="Portfolio amaliyot davri boshlangach shakllanadi."
        />
      );
    }
    return <ErrorState description={errorMessage(q.error)} onRetry={() => void q.refetch()} />;
  }
  const p = q.data;
  const selectedPeriod = p.periods.find((x) => x.id === p.periodId);
  const stats = [
    { k: 'Davomat', v: formatPercent(p.stats.attendancePct) },
    { k: 'Qatnashgan kunlar', v: `${p.stats.daysPresent}/${p.stats.daysTotal}` },
    { k: 'Kech qolgan', v: String(p.stats.late) },
    { k: 'Sababli', v: String(p.stats.excused) },
    { k: 'Hisobotlar', v: String(p.stats.reports) },
    { k: "O'rtacha ball", v: formatDecimal(p.stats.avgScore) },
  ];

  return (
    <div className={pages.stack} aria-busy={q.isPlaceholderData || undefined}>
      {p.periods.length >= 2 && (
        <PeriodPicker
          periods={p.periods}
          selectedId={periodId ?? p.periodId}
          onSelect={setPeriodId}
        />
      )}
      <Card padded="form" aria-labelledby="portfolio-title">
        <div className={styles.head}>
          <div className={styles.headText}>
            <h2 id="portfolio-title" className={styles.name}>
              {p.student} · {p.group}
            </h2>
            <div className={styles.sub}>
              {[p.practiceTitle, p.company, formatPeriod(p.periodFrom, p.periodTo)]
                .filter(Boolean)
                .join(' · ')}
            </div>
          </div>
          <Button
            variant="primary"
            radius="md2"
            className={styles.pdf}
            disabled={!p.pdfUrl}
            title={p.pdfUrl ? undefined : 'PDF amaliyot yakunida tayyorlanadi'}
            onClick={() => p.pdfUrl && openExternal(p.pdfUrl)}
          >
            Portfolio PDF
          </Button>
        </div>
        {selectedPeriod?.status === 'planned' && (
          <p className={styles.planned} role="status">
            Davr hali boshlanmagan — {formatDate(selectedPeriod.startDate)} dan boshlanadi.
          </p>
        )}
        <FactGrid items={stats} variant="portfolio" min={120} className={styles.stats} />
      </Card>

      <Card aria-label="Yakuniy baho hisobi">
        <CardHeader title="Yakuniy baho hisobi" />
        <ul className={styles.scoreList}>
          {p.score.map((s) => {
            const label = scoreLabel(s.key);
            return (
              <li key={s.key} className={styles.scoreRow}>
                <div className={styles.scoreLine}>
                  <span>
                    {label} · {s.weightPct}%
                  </span>
                  <span className={styles.scoreVal}>{formatDecimal(s.points)}</span>
                </div>
                <ProgressBar
                  value={s.weightPct > 0 ? (s.points / s.weightPct) * 100 : 0}
                  showValue={false}
                  label={`${label} — ${formatDecimal(s.points)} / ${s.weightPct}`}
                  className={styles.bar}
                />
              </li>
            );
          })}
        </ul>
        <CardFooter className={styles.total}>
          <span className={styles.totalText}>Jami · {formatDecimal(p.total)} ball</span>
          {p.grade !== null ? (
            <Badge
              status={p.grade >= 4 ? 'ok' : p.grade === 3 ? 'late' : 'bad'}
              size="lg"
              className={styles.grade}
            >
              Baho: {p.grade}
              {p.finalized ? '' : ' (dastlabki)'}
            </Badge>
          ) : (
            <Badge status="neu" size="lg" className={styles.grade}>
              Baho hali yo'q
            </Badge>
          )}
        </CardFooter>
      </Card>

      <Card padded aria-labelledby="conclusion-title">
        <h2 id="conclusion-title" className={pages.sectionTitle}>
          Tyutor xulosasi
        </h2>
        {p.conclusion ? (
          <>
            <p className={styles.conclusion}>{p.conclusion.text}</p>
            <div className={styles.conclusionMeta}>
              {p.conclusion.author} · {formatDate(p.conclusion.date)}
            </div>
          </>
        ) : (
          <p className={styles.conclusionEmpty}>
            Xulosa amaliyot yakunida tyutor tomonidan yoziladi.
          </p>
        )}
      </Card>
    </div>
  );
}

export default PortfolioPage;

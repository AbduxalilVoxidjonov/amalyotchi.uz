import { AppLink, EmptyState, Eyebrow } from '@/shared/ui';
import { formatDecimal, formatPercent } from '@/shared/lib/format';
import type { TodayPlaceDto } from '../types';
import styles from './PlaceSummary.module.css';

/** SPEC-SCREENS §8 o'ng section pastki qismi — "Korxonam" qisqacha + mini-statlar. `null` → ariza yo'q. */
export function PlaceSummary({ place }: { place: TodayPlaceDto | null }) {
  if (!place) {
    return (
      <section className={styles.wrap} aria-label="Korxonam">
        <Eyebrow as="div" margin="none">
          Korxonam
        </Eyebrow>
        <EmptyState
          title="Amaliyot joyi hali biriktirilmagan"
          description="Korxona tasdiqlangach, belgilanish ochiladi. Savol bo'lsa tyutorga murojaat qiling."
        />
      </section>
    );
  }
  const stats = [
    { k: 'Davomat', v: formatPercent(place.attendancePct) },
    { k: 'Kunlar', v: `${place.daysPresent}/${place.daysTotal}` },
    { k: 'Hisobotlar', v: String(place.reports) },
    { k: "O'rtacha ball", v: formatDecimal(place.avgScore) },
  ];
  return (
    <section className={styles.wrap} aria-label="Korxonam">
      <Eyebrow as="div" margin="none">
        Korxonam
      </Eyebrow>
      <AppLink to="/joyim" className={styles.company}>
        {place.company}
      </AppLink>
      <div className={styles.address}>
        {place.address} · radius {place.radiusM} m
      </div>
      <dl className={styles.stats}>
        {stats.map((s) => (
          <div key={s.k}>
            <dt className={styles.k}>{s.k}</dt>
            <dd className={styles.v}>{s.v}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

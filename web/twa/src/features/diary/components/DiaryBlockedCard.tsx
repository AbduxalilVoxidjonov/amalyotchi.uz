import { useId } from 'react';
import { Card } from '@/shared/ui';
import styles from './DiaryBlockedCard.module.css';

/** `upcoming` — davr hali boshlanmagan · `ended` — yakunlangan · `none` — davr biriktirilmagan. */
export type DiaryBlockedPhase = 'upcoming' | 'ended' | 'none';

export interface DiaryBlockedCardProps {
  phase: DiaryBlockedPhase;
  /** Server `diaryBlockedReason` (yo'q bo'lsa — standart matn). */
  reason: string | null;
  /** Oldingi yozuvlar bor — ular quyida o'qish uchun ko'rinadi. */
  hasEntries: boolean;
}

const TITLES = {
  upcoming: 'Amaliyot hali boshlanmagan',
  ended: 'Amaliyot yakunlangan',
  none: "Faol amaliyot davri yo'q",
} as const;

const FALLBACK_REASONS = {
  upcoming: 'Amaliyot davri hali boshlanmagan.',
  ended: "Amaliyot davri yakunlangan — yangi kundalik yozuvi qo'shib bo'lmaydi.",
  none: "Faol amaliyot davri yo'q — hisobot yozib bo'lmaydi.",
} as const;

/** `canWriteDiary=false` — kundalik formasi o'rniga ixcham ma'lumot kartasi. */
export function DiaryBlockedCard({ phase, reason, hasEntries }: DiaryBlockedCardProps) {
  const headingId = useId();
  return (
    <Card as="section" padded="lg" aria-labelledby={headingId}>
      <h2 id={headingId} className={styles.title}>
        {TITLES[phase]}
      </h2>
      <p className={styles.reason}>{reason || FALLBACK_REASONS[phase]}</p>
      {hasEntries && <p className={styles.hint}>Oldingi yozuvlaringiz quyida saqlangan.</p>}
    </Card>
  );
}

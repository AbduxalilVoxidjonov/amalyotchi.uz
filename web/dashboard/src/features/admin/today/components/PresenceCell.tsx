import { fmtDistance } from '../../shared/fmt';
import { fmtAgoShort, presenceChip } from '../presence';
import type { Presence } from '../types';
import styles from './PresenceCell.module.css';

export interface PresenceCellProps {
  presence: Presence | null | undefined;
  /** Test uchun almashtiriladi. */
  now?: number;
}

export const MOCKED_HINT = 'Soxta joylashuv aniqlangan';

/** "Joyida" katagi: holat chip'i + soxta joylashuv belgisi + oxirgi ping vaqti (nisbiy). */
export function PresenceCell({ presence, now }: PresenceCellProps) {
  if (!presence) return <span className={styles.empty}>—</span>;
  const chip = presenceChip(presence);
  const title =
    presence.distanceM !== null && presence.state !== 'stopped' && presence.state !== 'noConsent'
      ? `Korxonadan ${fmtDistance(presence.distanceM)}`
      : undefined;
  return (
    <span className={styles.cell}>
      <span className={styles.line}>
        <span
          className={styles.chip}
          data-tone={chip.tone}
          data-state={presence.state}
          title={title}
        >
          {chip.label}
        </span>
        {presence.mocked && (
          <span className={styles.mocked} role="img" aria-label={MOCKED_HINT} title={MOCKED_HINT}>
            !
          </span>
        )}
      </span>
      {presence.lastPingAt && (
        <span className={styles.ago}>{fmtAgoShort(presence.lastPingAt, now)}</span>
      )}
    </span>
  );
}

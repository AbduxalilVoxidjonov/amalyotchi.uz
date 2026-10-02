import { ProgressBar } from '@/shared/ui';
import { formatCount } from '../../shared/format';
import { processedPct } from '../format';
import type { MessageSummary } from '../types';
import styles from './Messages.module.css';

/** "12 / 44" + ishlanganlik chizig'i (accent rang — davomat ranglari bu yerda ma'nosiz). */
export function DeliveryProgress({ message }: { message: MessageSummary }) {
  return (
    <span className={styles.progress}>
      <span className={styles.progressText}>
        {formatCount(message.sent)} / {formatCount(message.total)}
      </span>
      <ProgressBar
        value={processedPct(message)}
        showValue={false}
        color="var(--color-accent)"
        label={`Yetkazildi: ${message.sent} / ${message.total}`}
      />
    </span>
  );
}

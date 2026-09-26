import { Button } from '@/shared/ui';
import { formatMeters } from '@/shared/lib/format';
import type { CheckinFlow } from '../hooks';
import styles from './LocationStep.module.css';

/** Sarlavha va izoh — joylashuv holati va so'rovdan. */
function describe(flow: CheckinFlow): { title: string; note: string } {
  const { location, point, pending, locationError, error } = flow;
  if (locationError) {
    return {
      title: 'Joylashuv tasdiqlanmadi',
      note: 'GPS yoqilganini va joylashuvga ruxsat berilganini tekshiring, korxona hududida qayta urinib ko‘ring.',
    };
  }
  if (error) return { title: 'Yuborib bo‘lmadi', note: 'Qayta urinib ko‘ring.' };
  if (pending) {
    return {
      title: 'Yuborilmoqda…',
      note: point ? `Joylashuv aniqlandi · aniqlik ±${formatMeters(point.accuracy)}` : '',
    };
  }
  if (location === 'ok' && point) {
    return {
      title: 'Joylashuv aniqlandi',
      note: `Aniqlik ±${formatMeters(point.accuracy)}`,
    };
  }
  return {
    title: 'Joylashuv aniqlanmoqda…',
    note: 'GPS orqali korxona hududida ekaningiz tekshiriladi. Ochiq joyda aniqlik yaxshiroq bo‘ladi.',
  };
}

/**
 * 3-qadam: joylashuv (presentation). Joylashuv selfi paytida fonda olingan bo'lishi mumkin — bu yerda
 * natija kutiladi va so'rov yuboriladi. Xato (ruxsat yo'q, GPS aniqligi yetarli emas, radius tashqarisi,
 * tarmoq) → aniq xabar va "Qayta urinish" (QR va selfi saqlanadi).
 */
export function LocationStep({ flow }: { flow: CheckinFlow }) {
  const { title, note } = describe(flow);
  const failed = flow.locationError !== null || flow.error !== null;
  const busy = !failed && (flow.pending || flow.location !== 'error');

  return (
    <section className={styles.panel} aria-label="Joylashuv" aria-busy={busy || undefined}>
      <h3 className={styles.title}>{title}</h3>
      {note && <p className={styles.note}>{note}</p>}

      {flow.locationError && (
        <p className={styles.error} role="alert">
          {flow.locationError}
        </p>
      )}
      {flow.error && (
        <p className={styles.error} role="alert">
          {flow.error}
        </p>
      )}

      {failed && (
        <div className={styles.actions}>
          <Button
            variant="primary"
            radius="md2"
            className={styles.action}
            onClick={flow.retry}
            disabled={flow.pending}
          >
            Qayta urinish
          </Button>
        </div>
      )}

      <button type="button" className={styles.cancel} onClick={flow.cancel} disabled={flow.pending}>
        Bekor qilish
      </button>
    </section>
  );
}

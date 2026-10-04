import { Link } from 'react-router-dom';
import { Alert, AlertList, AlertRow, Button } from '@/shared/ui';
import type { AlertModel } from '../present';
import styles from './AlertBanner.module.css';

export interface AlertBannerProps {
  alerts: readonly AlertModel[];
  onDismiss: () => void;
}

/** `showAlerts` sub-bloki (SPEC-SCREENS §17). Yopish tugmasi — ❓ dizaynda yo'q, lokal holat. */
export function AlertBanner({ alerts, onDismiss }: AlertBannerProps) {
  if (alerts.length === 0) return null;
  return (
    <Alert className={styles.alert} aria-label="Diqqat talab qiladi">
      <button
        type="button"
        className={styles.close}
        onClick={onDismiss}
        aria-label="Ogohlantirishlarni yopish"
      >
        ×
      </button>
      <AlertList>
        {alerts.map((a) => (
          <AlertRow
            key={a.id}
            action={
              <Button asChild variant="alert">
                <Link to={a.href}>{a.action}</Link>
              </Button>
            }
          >
            {a.text}
          </AlertRow>
        ))}
      </AlertList>
    </Alert>
  );
}

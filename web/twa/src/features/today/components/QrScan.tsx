import { Button } from '@/shared/ui';
import { env } from '@/shared/lib/env';
import type { CheckinFlow } from '../hooks';
import { MOCK_TEST_QR } from '../qr';
import { canUseCamera, qrScannerKind } from '../qr-scanner';
import styles from './QrScan.module.css';

/**
 * 1-qadam: amaliyot joyidagi QR kodni skanerlash (presentation). Telegram ichida — native popup,
 * brauzerda — kamera sheet (`CameraQrScanner`). "Test QR" — faqat mock rejimida (`VITE_USE_MOCKS`).
 */
export function QrScanPanel({ flow }: { flow: CheckinFlow }) {
  const { mode, scanning, qrError, pending } = flow;
  const kind = qrScannerKind();
  const cameraFallback = kind === 'outdated' && canUseCamera();
  const canScan = kind === 'telegram' || kind === 'camera';

  return (
    <section
      className={styles.panel}
      aria-label={mode === 'checkin' ? 'Kelganlikni tasdiqlash' : 'Ketganlikni tasdiqlash'}
    >
      <h3 className={styles.title}>Amaliyot joyidagi QR kodni skanerlang</h3>
      <p className={styles.note}>
        QR kod amaliyot joyida osilgan. Skanerlangach joylashuv aniqlanadi va selfi olinadi.
      </p>

      {qrError && (
        <p className={styles.error} role="alert">
          {qrError}
        </p>
      )}

      {(canScan || cameraFallback || env.useMocks) && (
        <div className={styles.actions}>
          {canScan && (
            <Button
              variant="primary"
              radius="md2"
              className={styles.action}
              onClick={() => flow.scanQr()}
              disabled={scanning || pending}
              aria-busy={scanning || undefined}
            >
              {scanning ? 'Skaner ochiq…' : 'QR kodni skanerlash'}
            </Button>
          )}
          {cameraFallback && (
            <Button
              variant="secondary"
              radius="md2"
              className={styles.action}
              onClick={() => flow.scanQr({ camera: true })}
              disabled={scanning || pending}
            >
              Kamera orqali skanerlash
            </Button>
          )}
          {env.useMocks && (
            <Button
              variant="dashed"
              radius="md2"
              className={styles.action}
              onClick={() => flow.acceptQr(MOCK_TEST_QR)}
              disabled={scanning || pending}
            >
              Test QR
            </Button>
          )}
        </div>
      )}

      <button type="button" className={styles.cancel} onClick={flow.cancel} disabled={pending}>
        Bekor qilish
      </button>
    </section>
  );
}

/** QR tasdiqlangan (faqat format bo'yicha — korxonaga tegishliligini server tekshiradi) + qayta skanerlash. */
export function QrConfirmed({ flow }: { flow: CheckinFlow }) {
  const busy = flow.pending || flow.preparing || flow.scanning;
  return (
    <div className={styles.confirmed} role="status">
      <span className={styles.check} aria-hidden="true">
        ✓
      </span>
      <span className={styles.confirmedText}>QR tasdiqlandi</span>
      <button type="button" className={styles.rescan} onClick={flow.rescanQr} disabled={busy}>
        Qayta skanerlash
      </button>
    </div>
  );
}

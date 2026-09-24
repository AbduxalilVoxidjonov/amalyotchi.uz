import { Button } from '@/shared/ui';
import { formatBytes } from '@/shared/lib/image';
import type { CheckinFlow } from '../hooks';
import styles from './SelfieCapture.module.css';

export interface SelfieCaptureProps {
  /** `useCheckinFlow` holati (container'dan). */
  flow: CheckinFlow;
  /** Kamerani ochadi — yashirin `input[type=file][capture]` `CheckinCard` da. */
  onOpenCamera: () => void;
}

/**
 * Check-in selfie paneli (kontrakt §1, 3-qadam) — presentation.
 * `capture` bosqichi: kamera kutilmoqda · `preview` bosqichi: rasm ko'rib chiqiladi (qayta olish/tasdiqlash).
 */
export function SelfieCapture({ flow, onOpenCamera }: SelfieCaptureProps) {
  const { phase, mode, photo, preparing, pending, error, photoError, requirements } = flow;
  const busy = preparing || pending;
  const hasPhoto = phase === 'preview' && photo !== null;

  return (
    <section
      className={styles.panel}
      aria-label={mode === 'checkin' ? 'Kelganlikni tasdiqlash' : 'Ketganlikni tasdiqlash'}
    >
      <h3 className={styles.title}>
        {mode === 'checkin' ? 'Selfie bilan tasdiqlang' : 'Ketishni selfie bilan tasdiqlang'}
      </h3>
      <p className={styles.note}>
        O‘zingizni suratga oling — rasm urinish bilan birga saqlanadi va tyutorga ko‘rinadi.
      </p>

      {hasPhoto ? (
        <figure className={styles.preview}>
          {photo.url ? (
            <img className={styles.previewImg} src={photo.url} alt="Olingan selfie" />
          ) : (
            <div className={styles.placeholder}>Rasm tayyor</div>
          )}
          <figcaption className={styles.meta}>
            Rasm tayyor · {formatBytes(photo.file.size)}
          </figcaption>
        </figure>
      ) : (
        <div className={styles.placeholder} data-busy={preparing || undefined}>
          {preparing ? 'Rasm tayyorlanmoqda…' : 'Rasm hali olinmagan'}
        </div>
      )}

      {photoError && (
        <p className={styles.error} role="alert">
          {photoError}
        </p>
      )}
      {error && (
        <p className={styles.error} role="alert">
          {error}
        </p>
      )}

      <div className={styles.actions}>
        {hasPhoto ? (
          <>
            <Button
              variant="secondary"
              radius="md2"
              className={styles.action}
              onClick={onOpenCamera}
              disabled={busy}
            >
              Qayta olish
            </Button>
            <Button
              variant="primary"
              radius="md2"
              className={styles.action}
              onClick={() => flow.submit()}
              disabled={busy}
              aria-busy={pending || undefined}
            >
              {pending ? 'Yuborilmoqda…' : 'Tasdiqlash va yuborish'}
            </Button>
          </>
        ) : (
          <>
            <Button
              variant="primary"
              radius="md2"
              className={styles.action}
              onClick={onOpenCamera}
              disabled={busy}
            >
              {preparing ? 'Tayyorlanmoqda…' : 'Rasmga olish'}
            </Button>
            {/* Sozlama `checkinPhotoRequired=false` bo'lsagina — aks holda server 400 `errors.Photo`. */}
            {!requirements.photoRequired && (
              <Button
                variant="dashed"
                radius="md2"
                className={styles.action}
                onClick={() => flow.submit({ withoutPhoto: true })}
                disabled={busy}
                aria-busy={pending || undefined}
              >
                {pending ? 'Yuborilmoqda…' : 'Rasmsiz davom etish'}
              </Button>
            )}
          </>
        )}
      </div>

      <button type="button" className={styles.cancel} onClick={flow.cancel} disabled={pending}>
        Bekor qilish
      </button>
    </section>
  );
}

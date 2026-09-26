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

/** Yuz uchun oval yo'naltiruvchi (kamera ochilishidan oldin — qanday suratga olish kerakligi). */
function FaceGuide({ busy }: { busy: boolean }) {
  return (
    <div className={styles.guide} data-busy={busy || undefined}>
      <svg
        className={styles.guideSvg}
        viewBox="0 0 120 150"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        aria-hidden="true"
      >
        <ellipse cx="60" cy="72" rx="42" ry="56" strokeDasharray="6 6" />
        <path d="M44 62h.01M76 62h.01M50 96c6 5 14 5 20 0" strokeWidth="3" />
      </svg>
      <span className={styles.guideText}>
        {busy ? 'Rasm tayyorlanmoqda…' : 'Yuzingiz oval ichida, yorug‘ joyda bo‘lsin'}
      </span>
    </div>
  );
}

/**
 * 2-qadam: yuz (selfi) — presentation. Old kamera (`capture="user"`), yuz uchun oval yo'naltiruvchi.
 * `capture` bosqichi: kamera kutilmoqda · `preview` bosqichi: rasm ko'rib chiqiladi (qayta olish/tasdiqlash).
 * "Tasdiqlash va yuborish" → 3-qadam (joylashuv) va so'rov. Yuzni tanish/solishtirish YO'Q — rasm faqat
 * urinish bilan saqlanadi va tyutorga ko'rinadi.
 */
export function SelfieCapture({ flow, onOpenCamera }: SelfieCaptureProps) {
  const { phase, mode, photo, preparing, pending, photoError, requirements } = flow;
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
        Old kamera ochiladi — yuzingizni suratga oling. Rasm urinish bilan birga saqlanadi va
        tyutorga ko‘rinadi. Keyingi qadamda joylashuvingiz aniqlanadi.
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
        <FaceGuide busy={preparing} />
      )}

      {photoError && (
        <p className={styles.error} role="alert">
          {photoError}
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

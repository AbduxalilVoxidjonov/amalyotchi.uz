import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useLogout } from '@/features/auth/hooks';
import { FacePhoto } from '@/features/face/components/FacePhoto';
import { useStudentFaceQuery, useSubmitFace } from '@/features/face/hooks';
import {
  FACE_CONSENT_TEXT,
  FACE_STATUS,
  needsFaceEnrollment,
  type StudentFaceDto,
} from '@/features/face/types';
import { FaceGuide } from '@/features/today/components/SelfieCapture';
import { PHOTO_ACCEPT, PHOTO_MESSAGES, preparePhoto } from '@/features/today/photo';
import { errorMessage, isApiError } from '@/shared/api/client';
import { formatDateTime } from '@/shared/lib/format';
import { createPreviewUrl, revokePreviewUrl } from '@/shared/lib/image';
import { AppLink, Badge, Button, Card, Checkbox, ErrorState, LoadingState } from '@/shared/ui';
import selfie from '@/features/today/components/SelfieCapture.module.css';
import pages from './pages.module.css';
import styles from './FaceEnrollPage.module.css';

const CLIENT_ERRORS = {
  photo: 'Avval yuzingizni suratga oling.',
  consent: 'Rozilik berilishi kerak.',
} as const;

/**
 * "Yuzni tasdiqlash" (`/face`, kontrakt v3.27 §6.33): talaba etalon selfi yuboradi (rozilik bilan) →
 * tyutor tasdiqlaydi yoki rad etadi. Holatlar: `none` → forma · `pending` → "Tyutor tekshirmoqda" ·
 * `approved` → tasdiqlangan · `rejected` → sabab + qayta yuborish formasi.
 * Darvoza (`required` va etalon yo'q/rad etilgan) holatida tab-bar yashirin — shu yerda "Chiqish" tugmasi bor.
 */
export function FaceEnrollPage() {
  const q = useStudentFaceQuery();

  if (q.isPending) return <LoadingState height={360} />;
  if (q.isError) {
    return <ErrorState description={errorMessage(q.error)} onRetry={() => void q.refetch()} />;
  }
  const face = q.data;
  const gated = needsFaceEnrollment(face);
  const canSubmit = face.status === 'none' || face.status === 'rejected';

  return (
    <div className={pages.stack}>
      <StatusCard face={face} gated={gated} />
      {canSubmit && <EnrollForm resubmit={face.status === 'rejected'} />}
      {gated && <GateLogout />}
    </div>
  );
}

function StatusCard({ face, gated }: { face: StudentFaceDto; gated: boolean }) {
  const meta = FACE_STATUS[face.status];
  return (
    <Card padded aria-labelledby="face-status-title">
      <div className={styles.head}>
        <h2 id="face-status-title" className={pages.sectionTitle}>
          Yuz rasmi
        </h2>
        <Badge status={meta.badge} size="sm">
          {meta.label}
        </Badge>
      </div>

      {face.status === 'none' && (
        <p className={styles.text}>
          {face.required
            ? 'Davomatni belgilashda selfingiz yuz rasmingiz bilan solishtiriladi. Davom etish uchun avval yuzingizni suratga olib yuboring — tyutoringiz tasdiqlaydi.'
            : 'Yuz rasmingizni yuborib qo‘yishingiz mumkin — tyutoringiz tasdiqlaydi.'}
        </p>
      )}
      {face.status === 'pending' && (
        <p className={styles.text} role="status">
          Rasmingiz yuborildi. Tyutor tekshirmoqda — tasdiqlangach davomatda yuzingiz shu rasm bilan
          solishtiriladi.
        </p>
      )}
      {face.status === 'approved' && (
        <p className={styles.text}>
          Yuz rasmingiz tasdiqlangan. Davomat selfisi shu rasm bilan solishtiriladi.
        </p>
      )}
      {face.status === 'rejected' && (
        <div className={styles.rejected} role="alert">
          <strong>Rasm rad etildi.</strong>
          {face.rejectReason && <span> Sabab: {face.rejectReason}</span>}
          <span> Yangi rasm yuboring.</span>
        </div>
      )}

      {face.photoUrl && face.status !== 'none' && (
        <div className={styles.photo}>
          <FacePhoto src={face.photoUrl} alt="Yuborilgan yuz rasmi" />
        </div>
      )}

      {(face.submittedAt || face.reviewedAt) && (
        <dl className={styles.meta}>
          {face.submittedAt && (
            <div>
              <dt>Yuborilgan</dt>
              <dd>{formatDateTime(face.submittedAt)}</dd>
            </div>
          )}
          {face.reviewedAt && (
            <div>
              <dt>Ko‘rib chiqilgan</dt>
              <dd>{formatDateTime(face.reviewedAt)}</dd>
            </div>
          )}
        </dl>
      )}

      {!gated && face.status !== 'none' && face.status !== 'rejected' && (
        <Button asChild variant="primary" radius="md2" block className={styles.home}>
          <AppLink to="/">Bosh ekranga</AppLink>
        </Button>
      )}
    </Card>
  );
}

interface Preview {
  file: File;
  url: string | null;
}

/** Etalon formasi: old kamera (`capture="user"`), oval yo'naltiruvchi, rozilik, yuborish. */
function EnrollForm({ resubmit }: { resubmit: boolean }) {
  const submit = useSubmitFace();
  const cameraRef = useRef<HTMLInputElement>(null);
  const previewRef = useRef<Preview | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [preparing, setPreparing] = useState(false);
  const [consent, setConsent] = useState(false);
  const [errors, setErrors] = useState<{
    photo?: string | undefined;
    consent?: string | undefined;
  }>({});

  useEffect(() => () => revokePreviewUrl(previewRef.current?.url), []);

  function setPreviewSafely(next: Preview | null) {
    revokePreviewUrl(previewRef.current?.url);
    previewRef.current = next;
    setPreview(next);
  }

  function openCamera() {
    const el = cameraRef.current;
    if (!el) return;
    el.value = '';
    el.click();
  }

  function onFile(file: File | null | undefined) {
    if (submit.isError) submit.reset();
    if (!file) {
      setErrors((e) => ({ ...e, photo: PHOTO_MESSAGES.cameraCancelled }));
      return;
    }
    setPreparing(true);
    setErrors((e) => ({ ...e, photo: undefined }));
    void preparePhoto(file)
      .then((prepared) => {
        if (!prepared.ok) {
          setErrors((e) => ({ ...e, photo: prepared.error }));
          return;
        }
        setPreviewSafely({ file: prepared.file, url: createPreviewUrl(prepared.file) });
      })
      .catch(() => setErrors((e) => ({ ...e, photo: PHOTO_MESSAGES.failed })))
      .finally(() => setPreparing(false));
  }

  function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = {
      photo: preview ? undefined : CLIENT_ERRORS.photo,
      consent: consent ? undefined : CLIENT_ERRORS.consent,
    };
    setErrors(next);
    if (!preview || !consent) return;
    submit.mutate({ photo: preview.file, consent: true });
  }

  const apiError = isApiError(submit.error) ? submit.error : null;
  const serverPhoto = apiError?.fieldError('photo');
  const serverConsent = apiError?.fieldError('consent');
  const generalError =
    submit.isError && !serverPhoto && !serverConsent ? errorMessage(submit.error) : null;
  const photoError = errors.photo ?? serverPhoto;
  const consentError = errors.consent ?? serverConsent;
  const busy = preparing || submit.isPending;

  return (
    <Card padded aria-labelledby="face-form-title">
      <h2 id="face-form-title" className={pages.sectionTitle}>
        {resubmit ? 'Rasmni qayta yuborish' : 'Yuzingizni suratga oling'}
      </h2>
      <p className={styles.text}>
        Old kamera ochiladi. Yuzingiz oval ichida, to‘liq va aniq ko‘rinsin: yorug‘ joy, ko‘zoynak
        va bosh kiyimsiz, kadrda faqat siz.
      </p>

      <form
        className={styles.form}
        onSubmit={onSubmit}
        noValidate
        aria-label="Yuz rasmini yuborish"
      >
        <input
          ref={cameraRef}
          type="file"
          hidden
          accept={PHOTO_ACCEPT}
          capture="user"
          aria-label="Yuz rasmini olish"
          onChange={(e) => onFile(e.target.files?.[0])}
        />

        {preview ? (
          <figure className={selfie.preview}>
            {preview.url ? (
              <img className={selfie.previewImg} src={preview.url} alt="Olingan yuz rasmi" />
            ) : (
              <div className={selfie.placeholder}>Rasm tayyor</div>
            )}
          </figure>
        ) : (
          <FaceGuide busy={preparing} />
        )}

        {photoError && (
          <p className={styles.error} role="alert">
            {photoError}
          </p>
        )}

        <Button
          type="button"
          variant={preview ? 'secondary' : 'primary'}
          radius="md2"
          block
          onClick={openCamera}
          disabled={busy}
        >
          {preparing ? 'Tayyorlanmoqda…' : preview ? 'Qayta olish' : 'Rasmga olish'}
        </Button>

        <Checkbox
          name="consent"
          label={FACE_CONSENT_TEXT}
          checked={consent}
          onChange={(e) => {
            setConsent(e.target.checked);
            setErrors((prev) => ({ ...prev, consent: undefined }));
          }}
          disabled={submit.isPending}
          aria-invalid={consentError ? true : undefined}
        />
        {consentError && (
          <p className={styles.error} role="alert">
            {consentError}
          </p>
        )}

        {generalError && (
          <p className={styles.error} role="alert">
            {generalError}
          </p>
        )}

        <Button
          type="submit"
          variant="primary"
          radius="md2"
          block
          disabled={busy}
          aria-busy={submit.isPending || undefined}
        >
          {submit.isPending ? 'Yuborilmoqda…' : 'Yuborish'}
        </Button>
      </form>
    </Card>
  );
}

/** Darvoza holatida tab-bar yo'q — hisobdan chiqish shu yerda. */
function GateLogout() {
  const logout = useLogout();
  return (
    <Button variant="danger" block onClick={() => logout.mutate()} disabled={logout.isPending}>
      {logout.isPending ? 'Chiqilmoqda…' : 'Chiqish'}
    </Button>
  );
}

export default FaceEnrollPage;

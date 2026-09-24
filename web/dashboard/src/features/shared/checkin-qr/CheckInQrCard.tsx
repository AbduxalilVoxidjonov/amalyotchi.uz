import { useEffect, useState } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { formatDateTime } from '../../admin/shared/format';
import { Button, Card, CardBody, CardHeader, ConfirmDialog } from '@/shared/ui';
import { useCheckinQrQuery, useRotateCheckinQr } from './hooks';
import {
  PRINT_HINT,
  buildPrintHtml,
  downloadDataUrl,
  fileSlug,
  printHtml,
  qrPngDataUrl,
  qrSvgDataUrl,
} from './qrImage';
import type { CheckInQrArea, CompanyCheckInQr } from './types';
import styles from './CheckInQrCard.module.css';

export interface CheckInQrCardProps {
  area: CheckInQrArea;
  companyId: string;
}

type ImageState = { payload: string; src: string | null; failed: boolean };

/** Payload o'zgarsa (rotatsiya) rasm qayta chiziladi. */
function useQrImage(payload: string | undefined): ImageState | null {
  const [state, setState] = useState<ImageState | null>(null);
  useEffect(() => {
    if (!payload) return undefined;
    let active = true;
    qrSvgDataUrl(payload)
      .then((src) => active && setState({ payload, src, failed: false }))
      .catch(() => active && setState({ payload, src: null, failed: true }));
    return () => {
      active = false;
    };
  }, [payload]);
  return state && state.payload === payload ? state : null;
}

const CARD_TITLE = 'Check-in QR kodi';

/**
 * Korxona check-in QR kartasi (admin va tyutor korxona sahifalari uchun umumiy).
 * QR rasmi `payload` dan client tomonda chiziladi; PNG yuklab olish, faqat QR + nom + ko'rsatma
 * bilan chop etish va rotatsiya (tasdiq oynasi bilan — eski QR bekor bo'ladi).
 */
export function CheckInQrCard({ area, companyId }: CheckInQrCardProps) {
  const query = useCheckinQrQuery(area, companyId);
  const rotate = useRotateCheckinQr(area, companyId);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const qr = query.data;

  const closeConfirm = () => {
    setConfirmOpen(false);
    rotate.reset();
  };

  return (
    <Card as="section" aria-label={CARD_TITLE}>
      <CardHeader
        title={CARD_TITLE}
        subtitle="Korxona kirishiga osib qo'ying — talaba kelish va ketishda skanerlaydi."
      />
      <CardBody>
        {query.isPending ? (
          <p className={styles.muted} role="status">
            Yuklanmoqda…
          </p>
        ) : query.isError ? (
          <div className={styles.error} role="alert">
            <p className={styles.errorTitle}>
              {isApiError(query.error) && query.error.status === 404
                ? 'QR kod topilmadi'
                : 'QR kodni yuklab bo‘lmadi'}
            </p>
            <p className={styles.muted}>
              {isApiError(query.error) && query.error.status === 404
                ? 'Bu korxona uchun QR kod mavjud emas yoki korxona ko‘lamingizda emas.'
                : errorMessage(query.error)}
            </p>
            <Button type="button" size="sm" onClick={() => void query.refetch()}>
              Qayta urinish
            </Button>
          </div>
        ) : (
          <QrContent
            qr={qr}
            actionError={actionError}
            onActionError={setActionError}
            onRotate={() => {
              setActionError(null);
              setConfirmOpen(true);
            }}
          />
        )}
      </CardBody>

      <ConfirmDialog
        open={confirmOpen}
        title="QR kodni yangilash"
        description="Eski QR kod ishlamay qoladi, yangisini chop etish kerak bo'ladi."
        confirmLabel="Yangilash"
        danger
        isLoading={rotate.isPending}
        error={rotate.error ? errorMessage(rotate.error) : undefined}
        onCancel={closeConfirm}
        onConfirm={() =>
          rotate.mutate(undefined, {
            onSuccess: () => setConfirmOpen(false),
          })
        }
      />
    </Card>
  );
}

function QrContent({
  qr,
  actionError,
  onActionError,
  onRotate,
}: {
  qr: CompanyCheckInQr | undefined;
  actionError: string | null;
  onActionError: (message: string | null) => void;
  onRotate: () => void;
}) {
  const image = useQrImage(qr?.payload);
  const [busy, setBusy] = useState<'download' | 'print' | null>(null);
  if (!qr) return null;

  const alt = `${qr.companyName} check-in QR kodi`;

  const run = async (kind: 'download' | 'print') => {
    setBusy(kind);
    onActionError(null);
    try {
      const png = await qrPngDataUrl(qr.payload);
      if (kind === 'download') downloadDataUrl(png, `checkin-qr-${fileSlug(qr.companyName)}.png`);
      else printHtml(buildPrintHtml(qr.companyName, png));
    } catch {
      onActionError(
        kind === 'download' ? 'PNG faylni tayyorlab bo‘lmadi.' : 'Chop etishni boshlab bo‘lmadi.',
      );
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className={styles.layout}>
      <figure className={styles.qrBox} data-payload={qr.payload}>
        {image?.src ? (
          <img className={styles.qr} src={image.src} alt={alt} />
        ) : (
          <div
            className={styles.qrPlaceholder}
            role={image?.failed ? 'img' : 'status'}
            aria-label={image?.failed ? `${alt} — chizib bo‘lmadi` : undefined}
          >
            {image?.failed ? 'QR chizilmadi' : 'Chizilmoqda…'}
          </div>
        )}
      </figure>

      <div className={styles.info}>
        <dl className={styles.facts}>
          <div>
            <dt>Korxona</dt>
            <dd>{qr.companyName}</dd>
          </div>
          <div>
            <dt>Oxirgi yangilanish</dt>
            <dd className={styles.mono}>
              <time dateTime={qr.rotatedAt}>{formatDateTime(qr.rotatedAt)}</time>
            </dd>
          </div>
          <div>
            <dt>Kod</dt>
            <dd className={styles.code} title={qr.payload}>
              {qr.payload}
            </dd>
          </div>
        </dl>
        <p className={styles.hint}>Chop etilgan varaqda: «{PRINT_HINT}».</p>

        <div className={styles.actions}>
          <Button
            type="button"
            size="sm"
            variant="primary"
            disabled={busy !== null}
            onClick={() => void run('download')}
          >
            {busy === 'download' ? 'Tayyorlanmoqda…' : 'Yuklab olish (PNG)'}
          </Button>
          <Button
            type="button"
            size="sm"
            disabled={busy !== null}
            onClick={() => void run('print')}
          >
            {busy === 'print' ? 'Tayyorlanmoqda…' : 'Chop etish'}
          </Button>
          <Button type="button" size="sm" variant="danger" onClick={onRotate}>
            Yangilash
          </Button>
        </div>
        {actionError && (
          <p className={styles.actionError} role="alert">
            {actionError}
          </p>
        )}
      </div>
    </div>
  );
}
